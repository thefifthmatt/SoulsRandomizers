using Avalonia.Platform.Storage;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using RandomizerCommon.Localization;
using RandomizerCommon.Models;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EnemyPreset;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Models.EnemyRepository;
using static RandomizerCommon.RandomizerOptions;
using static RandomizerCommon.ViewModels.EnemyPanelViewModel;
using static SoulsIds.Universe;

namespace RandomizerCommon.ViewModels
{
    public partial class EnemyPresetViewModel : ViewModelBase
    {
        public readonly IOptionsProvider OptionsProvider;
        public readonly EnemyRepository Repo;
        private readonly Messages messages;
        
        [Reactive]
        private ObservableCollection<EnemyPanelViewModel> _panels = new();
        [Reactive]
        private EnemyPanelViewModel? _selectedItem;
        
        [Reactive]
        private EnemyPreset _preset;
        // For now, still use this in a modal way, though integrated might be cool too
        [Reactive]
        private bool _modified;
        [Reactive]
        private EnemyPreset? _savedPreset;

        // Misc global stuff
        [Reactive] private bool _overallDlcSilo;
        [Reactive] private bool _dlcSiloForced;
        [Reactive] private DisplayEnemyList _norandom;
        [Reactive] private DisplayEnemyList _removeSource;
        [Reactive] private DisplayEnemyList _oopsAll;

        [Reactive] private bool _enableMultiplier;
        [Reactive] private int? _multiplier = 2;
        [Reactive] private bool _enableOopsAll;

        [Reactive] private ObservableCollection<OptionState> _miscOptions;

        // Calculated state
        [Reactive] private bool _norandomAll;
        [Reactive] private bool _isSeparate;
        [Reactive] private bool _isMergeMinor;
        [Reactive] private bool _isMergeBoss;

        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);

        [Localize]
        private static readonly Text titleText = new Text("Customize enemy placement", "PresetEditForm_PresetEditForm");
        [Localize]
        private static readonly Text invalidNameText = new Text("Invalid preset name: {0}", "PresetEditForm_invalidName");

        public EnemyPresetViewModel(EnemyRepository repo, EnemyPreset preset, IOptionsProvider optionsProvider)
        {
            Repo = repo;
            OptionsProvider = optionsProvider;
            Preset = preset.CloneConfiguration();
            messages = Messages.GetInstance();

            MiscOptions = new(PresetOptionsText.Select(e => new OptionState(e.Key, e.Value, PresetOptionsLabels.GetValueOrDefault(e.Key))));

            // TODO: Read dlcsilo
            RandomizerOptions current = optionsProvider.MakeOptions();
            DlcSiloForced = current["dlcsilo"];

            // Construct trees based on combination of enemy config (contains all required classes)
            // and the given preset. This model is the source of truth, rather than the preset.
            InitializePanels();

            Observable.Merge(
                this.WhenAnyValue(x => x.OverallDlcSilo).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.EnableMultiplier).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.Multiplier).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.EnableOopsAll).Select(_ => Unit.Default).Skip(1),
                MiscOptions.AsObservableChangeSet().WhenPropertyChanged(x => x.Enabled, false).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.OopsAll).SelectMany(DisplayEnemyList.WhenModified),
                this.WhenAnyValue(x => x.Norandom).SelectMany(DisplayEnemyList.WhenModified),
                this.WhenAnyValue(x => x.RemoveSource).SelectMany(DisplayEnemyList.WhenModified))
                .Subscribe(_ =>
                {
                    UpdateModel();
                });
            this.WhenAnyValue(x => x.Norandom).SelectMany(DisplayEnemyList.WhenValue)
                .Subscribe(_ => NorandomAll = Norandom.Items.Any(i => i.Obj == "AllEnemies"));
            this.WhenAnyValue(x => x.EnableMultiplier)
                .Subscribe(_ => PromptNorandomOnslaught());
            AvaloniaMessages.OnCultureChange().Subscribe(_ =>
            {
                UpdateText();
            });
            ClassifyMerge();
        }

        // Misc options are uniform so they can be in a list
        public partial class OptionState : ReactiveObject
        {
            [Reactive]
            private bool _enabled;
            // Immutable
            public string Name { get; }
            public Text Text { get; }
            public Text? Label { get; }

            public OptionState(string name, Text text, Text? label = null)
            {
                Name = name;
                Text = text;
                Label = label;
            }
        }

        public bool GetMultiplier(out int multiplier)
        {
            multiplier = Multiplier ?? 0;
            return EnableMultiplier && multiplier > 0;
        }

        public string? FromDisplayList(DisplayEnemyList? list)
        {
            if (list == null) return null;
            // At this point, we can check that the list only contains valid entries, but trust DisplayEnemy items are constructed correctly
            List<string> tags = list.Items
                .Select(i => i.Obj)
                .ToList();
            return tags.Count > 0 ? string.Join("; ", tags) : null;
        }

        private static readonly Dictionary<EnemyControlType, EnemyListType> customSourceTypes = new()
        {
            [EnemyControlType.ManualTarget] = EnemyListType.ManualPlacement,
            [EnemyControlType.ManualSource] = EnemyListType.ManualPlacement,
            [EnemyControlType.OopsAll] = EnemyListType.OopsAll,
        };

        public DisplayEnemyList ToDisplayList(
            EnemyControlType type, Text? label = null, EnemyPanelViewModel? panel = null,
            string? init = null, int? weight = null, bool randomByType = false)
        {
            if (type == EnemyControlType.ClassPool || type == EnemyControlType.AdjustSource)
            {
                // Non-null weight shows percent editor
                weight ??= 0;
            }
            IReadOnlyList<AutocompleteItem> sourceItems;
            if (type == EnemyControlType.ClassPool && panel?.Class is EnemyClass cl)
            {
                // This is created for every pool but at least the AutocompleteItem references are shared
                List<AutocompleteItem> classItems = Repo.GetAutocompleteItems(EnemyListType.EnemyPool).ToList();
                string defaultName = Repo.GetSelfName(cl);
                classItems[0] = new AutocompleteItem(defaultName, defaultName, "default");
                sourceItems = classItems;
            }
            else
            {
                sourceItems = Repo.GetAutocompleteItems(customSourceTypes.GetValueOrDefault(type, EnemyListType.Enemy));
            }
            List<DisplayEnemy> items = new();
            if (!string.IsNullOrWhiteSpace(init))
            {
                foreach (string tag in Util.PhraseRe.Split(init.Trim()))
                {
                    if (tag == "none") continue;
                    if (IsEntityId(tag, out _))
                    {
                        items.Add(new DisplayEnemy(tag, tag));
                    }
                    else
                    {
                        AutocompleteItem? opt = sourceItems.Where(i => i.Obj == tag).FirstOrDefault();
                        if (opt != null)
                        {
                            items.Add(new DisplayEnemy(opt.Obj, opt.Name));
                        }
#if DEBUG
                        else
                        {
                            Console.WriteLine($"Enemy preset tag ignored: [{tag}] in {type}");
                        }
#endif
                    }
                }
            }
            DisplayEnemySource source = new DisplayEnemySource(type, sourceItems, weight, label, randomByType);
            DisplayEnemyList list = new DisplayEnemyList(this, panel, source, items);
            return list;
        }

        public static bool IsEntityId(string text, out uint id)
        {
            ReadOnlySpan<char> sp = text.StartsWith('#') ? text.AsSpan(1) : text;
            // Max id, Furnace Golem 2251450280 in m61_12_11_02 -> m61_52_45_00
            return uint.TryParse(sp, out id) && id >= 100_000 && id < 2_300_000_000;
        }

        private void UpdateItems(Action<DisplayEnemy> action)
        {
            foreach (EnemyPanelViewModel child in Panels)
            {
                child.UpdateItems(action);
            }
            Norandom?.UpdateItems(action);
            RemoveSource?.UpdateItems(action);
        }

        private void UpdateText()
        {
            UpdateItems(item => item.UpdateText(Repo));
        }

        [Localize]
        private static readonly Text confirmMultiplyRandomText = new Text("Disable enemy randomization with Enemy Onslaught?", "PresetEditForm_confirmMultiplyRandom");

        private async void PromptNorandomOnslaught()
        {
            if (loading || NorandomAll || !EnableMultiplier)
            {
                return;
            }
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                messages.Get(FormText.ConfirmTitleText), messages.Get(confirmMultiplyRandomText),
                ButtonEnum.YesNo, Icon.Question, Avalonia.Controls.WindowStartupLocation.CenterOwner));
            if (result == ButtonResult.Yes && Norandom.Source.LookupEnemy("AllEnemies", out AutocompleteItem? item))
            {
                Norandom.AddItem(item);
            }
        }

        private bool loading = false;
        public void UpdateModel(bool preSave = false)
        {
            if (loading) return;

            if (!DlcSiloForced)
            {
                Preset.OverallSilo = OverallDlcSilo ? LocationData.AreaSiloType.DLC : LocationData.AreaSiloType.None;
            }
            Preset.DontRandomize = FromDisplayList(Norandom);
            Preset.RemoveSource = FromDisplayList(RemoveSource);
            Preset.OopsAll = EnableOopsAll ? FromDisplayList(OopsAll) : null;
            Preset.EnemyMultiplier = (EnableMultiplier ? Multiplier : null) ?? 0;
            foreach (OptionState option in MiscOptions)
            {
                Preset[option.Name] = option.Enabled;
            }
            foreach (EnemyPanelViewModel panel in Panels)
            {
                if (preSave)
                {
                    panel.PreSaveValidate();
                }
                if (panel.Type == TabType.EnemyCategories)
                {
                    Preset.Classes = panel.Children!
                        .Where(t => t.Type == TabType.EnemyCategory)
                        .ToDictionary(t => t.Class, t => t.GetModelObject<ClassAssignment>());
                }
                else if (panel.Type == TabType.AdjustSource)
                {
                    Preset.AdjustSource = panel.GetModelObject<List<SourceAdjustment?>>();
                }
                else if (panel.Type == TabType.ManualPlacement)
                {
                    Preset.Enemies = panel.GetModelObject<Dictionary<string, string>>();
                }
            }
            ClassifyMerge();
            Modified = true;
#if DEBUG
            Console.WriteLine(preSave ? "Saving enemy preset" : "Updating enemy preset");
#endif
        }

        // Sets panels based on current Preset
        private void InitializePanels()
        {
            loading = true;

            using (DelayChangeNotifications())
            {
                if (!DlcSiloForced)
                {
                    OverallDlcSilo = Preset.OverallSilo == LocationData.AreaSiloType.DLC;
                }
                Norandom = ToDisplayList(EnemyControlType.GlobalPool, dontRandomizeText, init: Preset.DontRandomize);
                RemoveSource = ToDisplayList(EnemyControlType.GlobalPool, removeSourceText, init: Preset.RemoveSource);
                OopsAll = ToDisplayList(EnemyControlType.OopsAll, init: Preset.OopsAll);
                EnableOopsAll = OopsAll.Items.Count > 0;
                Multiplier = Preset.EnemyMultiplier > 0 ? Preset.EnemyMultiplier : 2;
                EnableMultiplier = Preset.EnemyMultiplier > 0;
                foreach (OptionState option in MiscOptions)
                {
                    option.Enabled = Preset[option.Name];
                }
            }

            Panels = new()
            {
                new EnemyPanelViewModel(this, TabType.GlobalSettings),
                new EnemyPanelViewModel(this, TabType.MiscOptions),
                new EnemyPanelViewModel(this, TabType.EnemyCategories,
                    children: Repo.GetClasses().Select(c => new EnemyPanelViewModel(this, TabType.EnemyCategory, Preset.Classes?.GetValueOrDefault(c.Class), c.Class))),
                new EnemyPanelViewModel(this, TabType.AdjustSource, Preset.AdjustSource),
                new EnemyPanelViewModel(this, TabType.ManualPlacement, Preset.Enemies),
            };
            SelectedItem = Panels.First();

            loading = false;
        }

        // Window stuff which may get removed in the future
        public async Task<bool> ContinueAfterSave()
        {
            if (Modified)
            {
                ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ConfirmTitleText), messages.Get(FormText.ConfirmLeaveText),
                    ButtonEnum.YesNoCancel, Icon.Question, Avalonia.Controls.WindowStartupLocation.CenterOwner));
                if (result == ButtonResult.Yes)
                {
                    try
                    {
                        UpdateModel(true);
                        Preset.SavePreset();
                    }
                    catch (Exception ex)
                    {
                        await Modal(MessageBoxManager.GetMessageBoxStandard(
                            messages.Get(FormText.ErrorTitleText), messages.Get(FormText.SaveFailureText, Preset.FileName, ex),
                            ButtonEnum.Ok, Icon.Error, Avalonia.Controls.WindowStartupLocation.CenterOwner));
                        return false;
                    }
                    SavedPreset = Preset;
                    // In theory, immediately exiting, but just in case
                    Modified = false;
                }
                else if (result == ButtonResult.No)
                {
                    // Nothing to do
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        [ReactiveCommand]
        public async Task<EnemyPreset?> Ok()
        {
            // If this doesn't clear Modified, EnemyPresetWindow will call ContinueAfterSave
            await Apply();
            return SavedPreset;
        }

        [ReactiveCommand]
        public async Task<bool> Apply()
        {
            UpdateModel(true);
            try
            {
                Preset.SavePreset();
            }
            catch (Exception ex)
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ErrorTitleText), messages.Get(FormText.SaveFailureText, Preset.FileName, ex),
                    ButtonEnum.Ok, Icon.Error, Avalonia.Controls.WindowStartupLocation.CenterOwner));
                return false;
            }
            SavedPreset = Preset.CloneConfiguration();
            Modified = false;
            return true;
        }

        private void ImportPreset(EnemyPreset preset)
        {
            Preset = preset;
            InitializePanels();
            UpdateModel(true);
            Preset.Name = DefaultName;
            Preset.SavePreset();
            SavedPreset = Preset;
            Modified = false;
        }

        [ReactiveCommand]
        public async void Reset()
        {
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                messages.Get(FormText.ConfirmTitleText), messages.Get(FormText.ConfirmResetPresetText),
                ButtonEnum.OkCancel, Icon.Question, Avalonia.Controls.WindowStartupLocation.CenterOwner));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            ImportPreset(Repo.MakeDefault());
        }

        [ReactiveCommand]
        public void OpenDirectory()
        {
            DirectoryInfo dir = new("presets");
            if (dir.Exists)
            {
                Process.Start(new ProcessStartInfo(dir.FullName) { UseShellExecute = true });
            }
        }

        [ReactiveCommand]
        public async void Export()
        {
            if (!await ContinueAfterSave())
            {
                return;
            }
            if (MainWindow()?.StorageProvider is not IStorageProvider storageProvider)
            {
                return;
            }
            IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                FileTypeChoices = [FilePickerFileTypes.TextPlain, FilePickerFileTypes.All],
                ShowOverwritePrompt = true,
                SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync("presets"),
            });
            if (file?.TryGetLocalPath() is string path)
            {
                if (InvalidPresetNames.Contains(Path.GetFileNameWithoutExtension(path)))
                {
                    await Modal(MessageBoxManager.GetMessageBoxStandard(
                        messages.Get(FormText.ErrorTitleText), messages.Get(invalidNameText, path),
                        ButtonEnum.Ok, Icon.Error, Avalonia.Controls.WindowStartupLocation.CenterOwner));
                    return;
                }
                UpdateModel(true);
                File.WriteAllText(path, Preset.ToYamlString());
            }
        }

        [ReactiveCommand]
        public async void Import()
        {
            if (MainWindow()?.StorageProvider is not IStorageProvider storageProvider)
            {
                return;
            }
            IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                FileTypeFilter = [FilePickerFileTypes.TextPlain, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync("presets"),
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not string path)
            {
                return;
            }
            EnemyPreset replace;
            try
            {
                replace = LoadPresetFromPath(path);
                Repo.MigratePreset(replace);
            }
            catch (Exception ex)
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ErrorTitleText), messages.Get(FormText.LoadFailureText, path, ex),
                    ButtonEnum.Ok, Icon.Error, Avalonia.Controls.WindowStartupLocation.CenterOwner));
                return;
            }
            ImportPreset(replace);
        }

        // This is the merge corner, which may live in EnemyCategories after revamp
        private IEnumerable<EnemyPanelViewModel> GetCategoryPanels()
        {
            EnemyPanelViewModel? categories = Panels.Where(p => p.Type == TabType.EnemyCategories).FirstOrDefault();
            return categories?.Children?.Where(p => p.Type == TabType.EnemyCategory) ?? [];
        }

        private Dictionary<EnemyClass, PlacementMode> GetMergeModes()
        {
            return GetCategoryPanels().Where(p => p.Mode == PlacementMode.Merge || p.Mode == PlacementMode.Merge2).ToDictionary(p => p.Class, p => p.Mode);
        }

        private static readonly Dictionary<EnemyClass, PlacementMode> BossMergeModes = new()
        {
            [EnemyClass.MinorBoss] = PlacementMode.Merge,
            [EnemyClass.Miniboss] = PlacementMode.Merge,
            [EnemyClass.DragonMiniboss] = PlacementMode.Merge,
            [EnemyClass.NightMiniboss] = PlacementMode.Merge,
            [EnemyClass.Evergaol] = PlacementMode.Merge,
        };

        private static readonly Dictionary<EnemyClass, PlacementMode> MinorMergeModes = new()
        {
            [EnemyClass.Miniboss] = PlacementMode.Merge2,
            [EnemyClass.DragonMiniboss] = PlacementMode.Merge,
            [EnemyClass.NightMiniboss] = PlacementMode.Merge,
            [EnemyClass.Evergaol] = PlacementMode.Merge,
        };

        private void ClassifyMerge()
        {
            Dictionary<EnemyClass, PlacementMode> modes = GetMergeModes();
            IsSeparate = modes.Count == 0;
            IsMergeBoss = modes.Count == BossMergeModes.Count && !modes.Except(BossMergeModes).Any();
            IsMergeMinor = modes.Count == MinorMergeModes.Count && !modes.Except(MinorMergeModes).Any();
        }

        private void SetMerge(Dictionary<EnemyClass, PlacementMode> modes)
        {
            foreach (EnemyPanelViewModel p in GetCategoryPanels())
            {
                if (modes.TryGetValue(p.Class, out PlacementMode mergeMode))
                {
                    if (p.Modes != null && p.Modes.Contains(mergeMode))
                    {
                        p.Mode = mergeMode;
                    }
                }
                else if (p.Mode == PlacementMode.Merge || p.Mode == PlacementMode.Merge2)
                {
                    p.Mode = p.DefaultMode;
                }
            }
        }

        [ReactiveCommand]
        public void QuickSeparate() => SetMerge([]);

        [ReactiveCommand]
        public void QuickMergeBoss() => SetMerge(BossMergeModes);

        [ReactiveCommand]
        public void QuickMergeMinor() => SetMerge(MinorMergeModes);
    }
}