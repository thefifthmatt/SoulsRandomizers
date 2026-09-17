using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using RandomizerCommon;
using RandomizerCommon.Localization;
using RandomizerCommon.Models;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using static RandomizerCommon.ItemPreset;
using static RandomizerCommon.Messages;
using static SoulsIds.GameSpec;
using Application = Avalonia.Application;
using Icon = MsBox.Avalonia.Enums.Icon;

namespace EldenRingRandomizer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IOptionsProvider
    {
        private static readonly string ExeName = "eldenring.exe";
        private static readonly string DefaultExePath = @$"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\{ExeName}";
        private static string unseenTabs = "item2,enemy,dlc,misc";

        public static readonly string HelperConfigPath = @"dll\RandomizerHelper_config.ini";
        public static readonly string HelperLogPath = @"dll\RandomizerHelper_log.txt";

        private readonly Messages messages;
        private readonly ModRunner runner;
        private readonly string helperPath;
        private readonly string crashfixPath;
        
        [Reactive]
        private RandomizerOptions _options = new RandomizerOptions(FromGame.ER);
        // Only updated when randomizer is run
        private uint RunSeed { get; set; } = 0;
        private uint RunSeed2 { get; set; } = 0;
        [Reactive]
        private string _title = $"Elden Ring Randomizer {Randomizer.EldenVersion}";

        // --- Item randomizer (non-custom)
        [Reactive]
        private bool _item;
        [Reactive]
        private int _bias;
        [Reactive]
        private string _biasDesc;
        [Reactive]
        private string _biasLabel;

        // Only in simple mode
        [Reactive]
        private bool _default_keyloc = true;
        [Reactive]
        private bool _default_boss = true;
        [Reactive]
        private bool _raceloc_altboss;
        [Reactive]
        private bool _raceloc_health;
        [Reactive]
        private bool _raceloc_shops;
        [Reactive]
        private bool _raceloc_scadu;
        [Reactive]
        private bool nocaves;

        [Reactive]
        private int _keyloc_count;
        [Reactive]
        private int _boss_count;
        [Reactive]
        private int _altboss_count;
        [Reactive]
        private int _health_count;
        [Reactive]
        private int _shops_count;
        [Reactive]
        private int _scadu_count;

        [Reactive]
        private bool _racemode_key;
        [Reactive]
        private bool _norandom;
        [Reactive]
        private bool _default_key_any;
        [Reactive]
        private bool _racemode_health;
        [Reactive]
        private bool _norandom_health;
        [Reactive]
        private bool _default_health;
        [Reactive]
        private bool _racemode_upgrades;
        [Reactive]
        private bool _norandom_upgrades;
        [Reactive]
        private bool _default_upgrades;
        [Reactive]
        private bool _racemode_scadu;
        [Reactive]
        private bool _norandom_scadu;
        [Reactive]
        private bool _default_scadu;
        
        [Reactive]
        private bool _spellshops;
        [Reactive]
        private bool _dlcblessing;

        // Both simple and advanced
        [Reactive]
        private bool _weaponprogression;
        [Reactive]
        private bool _mats;
        [Reactive]
        private bool _copydrops;
        
        [Reactive]
        private bool _customitem;

        // --- Logic
        [Reactive]
        private int? _runes_end;
        [Reactive]
        private int? _runes_leyndell;
        [Reactive]
        private int? _runes_rold;
        [Reactive]
        private bool _runes_rold_default;

        [Reactive]
        private bool _rustykey;
        [Reactive]
        private bool _liurniagate;
        [Reactive]
        private bool _latedragonbarrow;
        [Reactive]
        private bool _altusgate;
        [Reactive]
        private bool _royalaccess;
        [Reactive]
        private bool _deathless;

        [Reactive]
        private bool _markareas;
        [Reactive]
        private bool _markitems;

        [Reactive]
        private bool _earlylegacy;
        [Reactive]
        private bool _earlymedal;
        [Reactive]
        private bool _moremaxslots;

        // Simple mode only
        [Reactive]
        private bool _dlckeysilo;
        [Reactive]
        private bool _gaolkeys;

        // May be simple in the future if arbitrary key splits are added
        [Reactive]
        private int? _messmershard;
        [Reactive]
        private bool _messmershard_default;
        [Reactive]
        private int? _shardreq;

        // --- Enemy Randomizer
        [Reactive]
        private bool _enemy;
        [Reactive]
        private bool _scale;
        [Reactive]
        private bool _phasehp;
        [Reactive]
        private bool _editnames;
        [Reactive]
        private bool _bossbgm;
        [Reactive]
        private bool _swaprewards;
        [Reactive]
        private bool _swapboss;
        [Reactive]
        private bool _nightday;
        [Reactive]
        private bool _ignoresize;

        [Reactive]
        private bool _customenemy;
        
        // --- DLC
        [Reactive]
        private bool _dlc;
        [Reactive]
        private bool _tarnished;
        [Reactive]
        private bool _dlcsilo_default;
        [Reactive]
        private bool _dlcsilo;

        [Reactive]
        private bool _maxweapon;
        [Reactive]
        private bool _woodsfast;
        [Reactive]
        private bool _nerflantern;
        [Reactive]
        private bool _springseals;

        [Reactive]
        private bool _dlcstart;
        [Reactive]
        private bool _dlcstart_default;
        [Reactive]
        private bool _quickstart;
        [Reactive]
        private bool _dlcshop;
        [Reactive]
        private bool _dlcpackage;
        [Reactive]
        private bool _packagelite;
        [Reactive]
        private bool _dlcroundtable;
        [Reactive]
        private int? _playerlevel;

        [Reactive]
        private int _package_count;

        [Reactive]
        private bool _maxflasknum;
        [Reactive]
        private bool _maxflask;
        [Reactive]
        private bool _maxpouch;
        [Reactive]
        private bool _maxmemslot;
        [Reactive]
        private bool _maxwhetblade;
        [Reactive]
        private bool _maxbb;

        // --- Misc
        [Reactive]
        private bool _invertstarting;
        [Reactive]
        private bool _onehand;
        [Reactive]
        private bool _nohand;
        [Reactive]
        private bool _default_twohand;
        [Reactive]
        private bool _changestats;
        [Reactive]
        private bool _dlconlyhand;
        [Reactive]
        private bool _invertgifts;
        [Reactive]
        private bool _invertoutfits;
        [Reactive]
        private bool _invertenvbgm;
        [Reactive]
        private bool _invertgestures;
        [Reactive]
        private bool _rainbow;

        [Reactive]
        private bool _weaponreqs;
        [Reactive]
        private bool _fog;
        [Reactive]
        private bool _crawl;
        [Reactive]
        private bool _nerfsh;
        [Reactive]
        private bool _randomania;

        [Reactive]
        private bool _allmaps;
        [Reactive]
        private bool _allcraft;
        [Reactive]
        private bool _nerfmalenia;
        [Reactive]
        private bool _nerfgargoyles;
        [Reactive]
        private bool _sombermode;
        [Reactive]
        private bool _snowfast;

        [Reactive]
        private bool _crashfix;
        [Reactive]
        private bool _helper;
        
        [Reactive]
        private bool _uxm;

        // Preset state
        [Reactive]
        private string _enemyPresetText = "";
        [Reactive]
        private string _itemPresetText = "";
        // Non-observable state set on updates
        private EnemyPreset? selectedEnemyPreset;
        private string? legacyEnemyPresetName;
        [Reactive]
        private ItemPresetViewModel? _itemPresetViewModel;

        [Reactive]
        private string _message = "";
        [Reactive]
        private bool _messageError = false;
        [Reactive]
        private string _status = "";
        [Reactive]
        private bool _statusFailure = false;
        [Reactive]
        private bool _statusSuccess = false;
        
        [Reactive]
        private string _exe = "";
        [Reactive]
        private string _mod = "";
        private string PrevMod = "";
        [Reactive]
        private string _dllStr = "";
        [Reactive]
        private ImmutableList<string> _dlls = [];
        [Reactive]
        private string _seed = "";
        [Reactive]
        private bool _hideseed = false;
        [Reactive]
        private char _passwordChar;
        [Reactive]
        private bool _rerollSeed = false;
        [Reactive]
        private bool _mustRerollSeed = false;
        [Reactive]
        private string _seed2 = "";
        [Reactive]
        private bool _rerollSeed2 = false;
        [Reactive]
        private bool _mustRerollSeed2 = false;
        [Reactive]
        private Text _randomizeText;
        [Reactive]
        private bool _working = false;
        [Reactive]
        private bool _launching = false;
        // TODO: Figure out a cleaner way to do this
        [Reactive]
        private bool _canLaunch = false;
        private IObservable<bool> launchEnabled;
        [Reactive]
        private bool _collect = false;
        private bool showedMismatchWarning;
        
        // TODO: Update this when culture changes. And language should be a property
        [Reactive]
        private string? _translators;
        [Reactive]
        private AvaloniaList<LangOption> _languages;

        public partial class LangOption : ReactiveObject
        {
            [Reactive]
            public bool _isSelected;
            public string Name { get; init; }
            public CultureInfo Culture { get; init; }

            public LangOption(string name, CultureInfo culture, string selected)
            {
                Name = name;
                Culture = culture;
                IsSelected = name == selected;
            }
        }

        // Simple Text/string pairs for fields which must be string and not Message.
        // A converter could be used but it would need to be a multi-converter to change when the language changes, so this kind of boilerplate is preferable.
        [Localize] private static readonly Text enemyseedPlaceholderText = new Text("(same as overall seed)", "EldenForm_enemyseedPlaceholder");
        public IObservable<string> EnemyseedPlaceholder { get; } = AvaloniaMessages.FromText(enemyseedPlaceholderText);
        [Localize] private static readonly Text exePlaceholderText = new Text("eldenring.exe path", "EldenForm_exePlaceholder");
        public IObservable<string> ExePlaceholder { get; } = AvaloniaMessages.FromText(exePlaceholderText);
        [Localize] private static readonly Text mergePlaceholderText = new Text("Other mod path (optional)", "EldenForm_mergePlaceholder");
        public IObservable<string> MergePlaceholder { get; } = AvaloniaMessages.FromText(mergePlaceholderText);
        // Used to be a label, got its colon removed
        [Localize] private static readonly Text seedPlaceholderText = new Text("Overall seed", "EldenForm_fixedseedL");
        public IObservable<string> SeedPlaceholder { get; } = AvaloniaMessages.FromText(seedPlaceholderText);
        [Localize] private static readonly Text racelocShowText = new Text("Show location list", "EldenForm_racelocShow");
        public IObservable<string> RacelocTooltip { get; } = AvaloniaMessages.FromText(racelocShowText);

        public Interaction<OptionsViewModel, OptionsViewModel.Result?> ShowOptionsDialog { get; } = new();
        public Interaction<MergeModViewModel, string?> ShowMergeModDialog { get; } = new();
        public Interaction<DllViewModel, ImmutableList<string>?> ShowDllDialog { get; } = new();
        public Interaction<EnemyPresetViewModel, EnemyPreset?> ShowEnemyPreset { get; } = new();
        public Interaction<Unit, Unit?> ShowHelperWindow { get; } = new();

        private readonly string DefaultOpts = "allmaps bossbgm changestats crashfix dlc dlcblessing dlckeysilo earlylegacy editnames enemy item mats phasehp raceloc_health raceloc_scadu raceloc_shops racemode_health racemode_key racemode_scadu racemode_upgrades scale spellshops weaponprogression bias:20";

        public MainWindowViewModel()
        {
            messages = Messages.GetInstance();
            runner = new ModRunner(FromGame.ER, "config_eldenringrandomizer.toml", @"diste\ModEngine\modengine2_launcher.exe");
            helperPath = new FileInfo(@"dll\RandomizerHelper.dll").FullName;
            crashfixPath = new FileInfo(@"dll\RandomizerCrashFix.dll").FullName;
            string optStr = RandomizerOptions.ReadOptions();
            if (string.IsNullOrEmpty(optStr))
            {
                optStr = DefaultOpts;
            }
            SetOptionsFromString(optStr);
            string? exeStr = RandomizerOptions.ReadExe();
            if (string.IsNullOrEmpty(exeStr) || !IsValidExe(exeStr))
            {
                exeStr = null;
            }
            Exe = exeStr ?? GetHeuristicExe() ?? "";
            if (RandomizerOptions.ReadExtraDlls() is List<string> extraDlls)
            {
                Dlls = ImmutableList.CreateRange(extraDlls);
            }
            if (RandomizerOptions.ReadViewedTabs() != unseenTabs)
            {
                // TODO: Blink
            }
            SyncDllOptions(false);
            // Don't validate if it was valid at some point in the past
            PrevMod = Mod = RandomizerOptions.ReadMod() ?? "";
            launchEnabled = this.WhenAnyValue(x => x.CanLaunch, c => (bool)c);
            // Language pref was already loaded before app initialization
            string language = messages.GetCurrentLanguageName();
            Languages = new(messages.GetLanguageList().Select(l => new LangOption(l.Item1, l.Item2, language)));
            Translators = messages.GetCurrentLanguage()?.Translators;

            // Note ToPropety and ObservableAsProperty appear to be broken in this setup, so just do manual subscriptions for everything
            // All variables which feed into MakeOptions aside from presets, which may require loading things to enable
            Observable.Merge(
                // Item
                this.WhenAnyValue(x => x.Item).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Bias).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_altboss).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_health).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_shops).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_scadu).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nocaves).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Racemode_key).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Norandom).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Default_key_any).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Racemode_health).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Norandom_health).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Default_health).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Racemode_upgrades).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Norandom_upgrades).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Default_upgrades).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Racemode_scadu).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Norandom_scadu).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Default_scadu).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Spellshops).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcblessing).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Weaponprogression).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Mats).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Copydrops).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Customitem).Select(_ => Unit.Default),
                // Logic
                this.WhenAnyValue(x => x.Runes_end).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Runes_leyndell).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Runes_rold).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Runes_rold_default).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Rustykey).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Liurniagate).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Latedragonbarrow).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Altusgate).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Royalaccess).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Deathless).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Markareas).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Markitems).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Earlylegacy).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Earlymedal).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Moremaxslots).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlckeysilo).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Gaolkeys).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Messmershard).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Messmershard_default).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Shardreq).Select(_ => Unit.Default),
                // Enemy
                this.WhenAnyValue(x => x.Enemy).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Scale).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Phasehp).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Editnames).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Bossbgm).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Swaprewards).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Swapboss).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nightday).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Ignoresize).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Customenemy).Select(_ => Unit.Default),
                // DLC
                this.WhenAnyValue(x => x.Dlc).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Tarnished).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcsilo_default).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcsilo).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxweapon).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Woodsfast).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nerflantern).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Springseals).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcstart).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcstart_default).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Quickstart).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcshop).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcpackage).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Packagelite).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlcroundtable).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Playerlevel).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxflasknum).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxflask).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxpouch).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxmemslot).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxwhetblade).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Maxbb).Select(_ => Unit.Default),
                // Misc
                this.WhenAnyValue(x => x.Invertstarting).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Onehand).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nohand).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Default_twohand).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Changestats).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlconlyhand).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Invertgifts).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Invertoutfits).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Invertenvbgm).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Invertgestures).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Rainbow).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Weaponreqs).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Fog).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Crawl).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nerfsh).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Randomania).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Allmaps).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Allcraft).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nerfmalenia).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nerfgargoyles).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Sombermode).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Snowfast).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Crashfix).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Helper).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Uxm).Select(_ => Unit.Default))
                .Select(_ => MakeOptions())
                .Subscribe(opt =>
                {
                    Options = opt;
                });
            Languages.AsObservableChangeSet().WhenAnyPropertyChanged(nameof(LangOption.IsSelected))
                .Subscribe(_ =>
                {
                    // IsSelected exclusivity is controlled by ToggleType=Radio
                    LangOption? selected = Languages.Where(l => l.IsSelected).FirstOrDefault();
                    if (selected != null)
                    {
                        messages.SetCulture(selected.Culture);
                        Translators = messages.GetCurrentLanguage()?.Translators;
                    }
                });
            // CultureChanged must have initial value to work with CombineLatest
            IObservable<Unit> langChange = AvaloniaMessages.OnCultureState();
            this.WhenAnyValue(x => x.Options, x => x.Translators, x => x.Exe).CombineLatest(langChange)
                .Subscribe(_ =>
                {
                    SetStatus(GetDefaultStatus(Options));
                    // Error could be unset in other cases (Exe change?)
                    DoBasicCheck(out string err);
                    SetError(err);
                    // This adds RunSeed, hopefully not that expensive
                    RandomizerOptions saved = OptionsWithSeed();
                    RandomizerOptions.SaveOptions(saved);
                });
            this.WhenAnyValue(x => x.Customitem)
                .Subscribe(_ =>
                {
                    if (Customitem)
                    {
                        // Only do this interactive thing when clicking the checkbox. Don't do it when loading into the form
                        // checkbox/button state are set in SetItemPreset, so no need to do that here
                        SetItemPreset(ItemPresetViewModel?.Preset, createIfAbsent: true, promptUpdate: true);
                        if (ItemPresetViewModel == null)
                        {
                            Customitem = false;
                        }
                    }
                    else
                    {
                        SetItemPreset(null);
                    }
                });
            // Hardcode options used in SwitchOn/SwitchOff
            this.WhenAnyValue(x => x.Dlc, x => x.Dlcstart)
                .Subscribe(_ =>
                {
                    if (ItemPresetViewModel != null)
                    {
                        ItemPresetViewModel.UpdateFromOptions(MakeOptions());
                    }
                });
            // Sync Bias with item preset when both are defined
            this.WhenAnyValue(x => x.Bias, x => x.ItemPresetViewModel)
                .Subscribe(_ =>
                {
                    if (ItemPresetViewModel != null)
                    {
                        ItemPresetViewModel.Bias = Bias;
                    }
                });
            this.WhenAnyValue(x => x.ItemPresetViewModel!.Bias)
                .Subscribe(_ =>
                {
                    if (ItemPresetViewModel != null)
                    {
                        Bias = ItemPresetViewModel.Bias;
                    }
                });
            this.WhenAnyValue(x => x.Customenemy).CombineLatest(langChange)
                .Subscribe(_ =>
                {
                    if (Customenemy)
                    {
                        SetEnemyPreset(selectedEnemyPreset, true);
                        if (selectedEnemyPreset == null)
                        {
                            Customenemy = false;
                        }
                    }
                    else
                    {
                        SetEnemyPreset(null);
                    }
                });
            // Misc constraints
            this.WhenAnyValue(x => x.Runes_rold_default, x => x.Messmershard_default)
                .Subscribe(_ =>
                {
                    if (Runes_rold_default)
                    {
                        Runes_rold ??= RandomizerOptions.IntOpt.RunesRold.Def;
                    }
                    if (Messmershard_default)
                    {
                        Messmershard ??= RandomizerOptions.IntOpt.Messmershard.Def;
                        Shardreq ??= Messmershard;
                    }
                });
            this.WhenAnyValue(x => x.Default_keyloc, x => x.Default_boss)
                .Subscribe(_ =>
                {
                    Default_keyloc = true;
                    Default_boss = true;
                });
            // Make the relationship between these pairs more obvious. This could also be done as a style rule.
            // Other things are disabled in the UI without unchecking the option. In all cases, randomizer *should* always check for first before checking the second.
            this.WhenAnyValue(x => x.Markareas, markareas => !(bool)markareas)
                .Subscribe(_ => Markitems = false);
            this.WhenAnyValue(x => x.Fog, fog => !(bool)fog)
                .Subscribe(_ => Crawl = false);
            // Labels
            this.WhenAnyValue(x => x.Bias).CombineLatest(langChange).Subscribe(_ => UpdateBiasText());
            this.WhenAnyValue(x => x.Packagelite).Subscribe(_ => Package_count = Packagelite ? 50 : 80);
            Observable.Merge(
                this.WhenAnyValue(x => x.Item).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Bias).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_altboss).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_health).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_shops).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Raceloc_scadu).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Nocaves).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.Dlc).Select(_ => Unit.Default))
                .Subscribe(_ =>
                {
                    Keyloc_count = GetLocationCount("default_keylocL");
                    Boss_count = GetLocationCount("default_bossL");
                    Altboss_count = GetLocationCount("raceloc_altbossL");
                    Health_count = GetLocationCount("raceloc_healthL");
                    Shops_count = GetLocationCount("raceloc_shopsL");
                    Scadu_count = GetLocationCount("raceloc_scaduL");
                });
            // Seeds
            this.WhenAnyValue(x => x.Hideseed)
                // Wider: ●
                .Subscribe(_ => PasswordChar = Hideseed ? '•' : '\0');
            this.WhenAnyValue(x => x.Seed)
                .Subscribe(_ =>
                {
                    bool valid = uint.TryParse(Seed, out uint val) && val != 0;
                    MustRerollSeed = RerollSeed = !valid;
                    DoBasicCheck(out string err);
                    SetError(err);
                });
            this.WhenAnyValue(x => x.Seed2)
                .Subscribe(_ =>
                {
                    bool valid = uint.TryParse(Seed2, out uint val) && val != 0;
                    MustRerollSeed2 = RerollSeed2 = !string.IsNullOrWhiteSpace(Seed2) && !valid;
                });
            this.WhenAnyValue(x => x.Working, x => x.Item, x => x.Enemy, x => x.RerollSeed, x => x.RerollSeed2, x => x.Seed2).CombineLatest(langChange)
                .Subscribe(_ => RandomizeText = GetRandomizeButtonText());
            this.WhenAnyValue(x => x.Crashfix, x => x.Helper)
                .Subscribe(_ =>
                {
                    ImmutableList<string> prevDlls = Dlls;
                    SyncDllOptions(false);
                    // This happens on startup without this check
                    if (!Dlls.SequenceEqual(prevDlls))
                    {
                        runner?.DeleteLaunchFile();
                    }
                    UpdateCanLaunch();
                });
            this.WhenAnyValue(x => x.Dlls).CombineLatest(langChange)
                .Subscribe(_ =>
                {
                    DllStr = Dlls.Count == 0 ? "" : messages.Get(dllList, string.Join(", ", Dlls.Select(Path.GetFileName).Distinct()));
                    RandomizerOptions.SaveExtraDlls(Dlls);
                });
            this.WhenAnyValue(x => x.Exe)
                .Subscribe(exe =>
                {
                    if (IsValidExe(exe))
                    {
                        RandomizerOptions.SaveExe(exe);
                    }
                });
            this.WhenAnyValue(x => x.Mod)
                .Subscribe(mod =>
                {
                    if (mod == "" || File.Exists(mod) || Directory.Exists(mod))
                    {
                        RandomizerOptions.SaveMod(mod);
                    }
                    if (mod != PrevMod)
                    {
                        PrevMod = mod;
                        runner?.DeleteLaunchFile();
                        UpdateCanLaunch();
                    }
                });
            // Attempt to GC after randomization. Unfortunately this doesn't actually reclaim any memory.
            this.WhenAnyValue(x => x.Collect, collect => (bool)collect)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(_ =>
                {
                    Collect = false;
                    Task.Factory.StartNew(() =>
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    });
                });

            Observable.FromEventPattern(runner, "StartRunning").Subscribe(_ => Launching = true);
            Observable.FromEventPattern(runner, "DoneRunning").Subscribe(_ => Launching = false);
            Observable.FromEventPattern(runner, "FailedToStart").Subscribe(async _ => {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(launchFailedTitle),
                    messages.Get(launchFailed),
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
            });

            UpdateCanLaunch();
        }
        
        private bool DoBasicCheck(out string err)
        {
            // Returns true if no fatal errors
            return MiscSetup.CheckRequiredEldenFiles(messages, out err)
                && !MiscSetup.CheckEldenRingMods(messages, Uxm, Exe, out err);
        }

        public async Task StartView()
        {
            if (messages.InitErrors.Count > 0)
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    "Invalid messages",
                    string.Join(Environment.NewLine, messages.InitErrors),
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
            }
            // EditCustomEnemy();
        }
        
        private void SetOptionsFromString(string optStr, bool clearEmptySeed = false)
        {
            // For now, don't validate much
            RandomizerOptions opt = RandomizerOptions.Parse(optStr.Split(' '), FromGame.ER);
            // Version migration logic goes here, last done for crashfix in v14
            if (opt.Seed != 0)
            {
                RunSeed = opt.Seed;
                Seed = RunSeed.ToString();
            }
            else if (clearEmptySeed)
            {
                RunSeed = 0;
                Seed = "";
            }
            if (opt.Seed2 != 0)
            {
                RunSeed2 = opt.Seed2;
                Seed2 = RunSeed2.ToString();
            }
            else if (clearEmptySeed)
            {
                RunSeed2 = 0;
                Seed2 = "";
            }
            SetOptions(opt);
        }

        private void SetOptions(RandomizerOptions opt)
        {
            // Item
            Item = opt["item"];
            Bias = opt.Difficulty;
            Default_keyloc = true;
            Default_boss = true;
            Raceloc_altboss = opt["raceloc_altboss"];
            Raceloc_health = opt["raceloc_health"];
            Raceloc_shops = opt["raceloc_shops"];
            Raceloc_scadu = opt["raceloc_scadu"];
            Nocaves = opt["nocaves"];
            Racemode_key = opt["racemode_key"];
            Default_key_any = !opt["racemode_key"] && !opt["norandom"];
            Norandom = opt["norandom"];
            Racemode_health = opt["racemode_health"];
            Norandom_health = opt["norandom_health"];
            Default_health = !opt["racemode_health"] && !opt["norandom_health"];
            Racemode_upgrades = opt["racemode_upgrades"];
            Norandom_upgrades = opt["norandom_upgrades"];
            Default_upgrades = !opt["racemode_upgrades"] && !opt["norandom_upgrades"];
            Racemode_scadu = opt["racemode_scadu"];
            Norandom_scadu = opt["norandom_scadu"];
            Default_scadu = !opt["racemode_scadu"] && !opt["norandom_scadu"];
            Spellshops = opt["spellshops"];
            Dlcblessing = opt["dlcblessing"];
            Weaponprogression = opt["weaponprogression"];
            Mats = opt["mats"];
            Copydrops = opt["copydrops"];
            // This loads the item preset from Customitem subscription above
            Customitem = opt["customitem"];
            // Logic
            // TODO: Make this always enabled, or tie it to item randomizer being enabled
            Runes_end = opt.GetIntOrDefault(RandomizerOptions.IntOpt.RunesEnd);
            Runes_leyndell = opt.GetIntOrDefault(RandomizerOptions.IntOpt.RunesLeyndell);
            Runes_rold_default = opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out int runesRold);
            Runes_rold = Runes_rold_default ? runesRold : null;
            Rustykey = opt["rustykey"];
            Liurniagate = opt["liurniagate"];
            Latedragonbarrow = opt["latedragonbarrow"];
            Altusgate = opt["altusgate"];
            Royalaccess = opt["royalaccess"];
            Deathless = opt["deathless"];
            Markareas = opt["markareas"];
            Markitems = opt["markitems"];
            Earlylegacy = opt["earlylegacy"];
            Earlymedal = opt["earlymedal"];
            Moremaxslots = opt["moremaxslots"];
            Dlckeysilo = opt["dlckeysilo"];
            Gaolkeys = opt["gaolkeys"];
            Messmershard_default = opt.GetInt(RandomizerOptions.IntOpt.Messmershard, out int messmerShards);
            if (Messmershard_default)
            {
                Messmershard = messmerShards;
                Shardreq = opt.GetInt(RandomizerOptions.IntOpt.Shardreq, out int shardreq) ? shardreq : messmerShards;
            }
            else
            {
                Messmershard = Shardreq = null;
            }
            // Enemy
            Enemy = opt["enemy"];
            Scale = opt["scale"];
            Phasehp = opt["phasehp"];
            Editnames = opt["editnames"];
            Bossbgm = opt["bossbgm"];
            Swaprewards = opt["swaprewards"];
            Swapboss = opt["swapboss"];
            Nightday = opt["nightday"];
            Ignoresize = opt["ignoresize"];
            // Migrate from presets having names, which is not as relevant with a custom editor
            // If it would be helpful, create a dialog which allows selecting a preset by name and then copying it into CustomEnemy
            legacyEnemyPresetName = opt.Preset;
            Customenemy = opt["customenemy"] || opt.Preset != null;
            // DLC
            Dlc = opt["dlc"];
            Tarnished = opt["tarnished"];
            Dlcsilo = opt["dlcsilo"];
            Dlcsilo_default = !opt["dlcsilo"];
            Maxweapon = opt["maxweapon"];
            Woodsfast = opt["woodsfast"];
            Nerflantern = opt["nerflantern"];
            Springseals = opt["springseals"];
            Dlcstart = opt["dlcstart"];
            Dlcstart_default = !opt["dlcstart"];
            Quickstart = opt["quickstart"];
            Dlcshop = opt["dlcshop"];
            Dlcpackage = opt["dlcpackage"];
            Packagelite = opt["packagelite"];
            Dlcroundtable = opt["dlcroundtable"];
            Playerlevel = opt.GetIntOrDefault(RandomizerOptions.IntOpt.PlayerLevel);
            Maxflasknum = opt["maxflasknum"];
            Maxflask = opt["maxflask"];
            Maxpouch = opt["maxpouch"];
            Maxmemslot = opt["maxmemslot"];
            Maxwhetblade = opt["maxwhetblade"];
            Maxbb = opt["maxbb"];
            // Misc
            Invertstarting = opt["invertstarting"];
            Onehand = opt["onehand"];
            Nohand = opt["nohand"];
            Default_twohand = !opt["onehand"] && !opt["nohand"];
            Changestats = opt["changestats"];
            Dlconlyhand = opt["dlconlyhand"];
            Invertgifts = opt["invertgifts"];
            Invertoutfits = opt["invertoutfits"];
            Invertenvbgm = opt["invertenvbgm"];
            Invertgestures = opt["invertgestures"];
            Rainbow = opt["rainbow"];
            Weaponreqs = opt["weaponreqs"];
            Fog = opt["fog"];
            Crawl = opt["crawl"];
            Nerfsh = opt["nerfsh"];
            Randomania = opt["randomania"];
            Allmaps = opt["allmaps"];
            Allcraft = opt["allcraft"];
            Nerfmalenia = opt["nerfmalenia"];
            Nerfgargoyles = opt["nerfgargoyles"];
            Sombermode = opt["sombermode"];
            Snowfast = opt["snowfast"];
            Crashfix = opt["crashfix"];
            Helper = opt["helper"];
            Uxm = opt["uxm"];
            Hideseed = Hideseed || opt["hideseed"];
        }

        public RandomizerOptions MakeDefaultOptions()
        {
            return RandomizerOptions.Parse(DefaultOpts.Split(' '), FromGame.ER);
        }
        
        public RandomizerOptions MakeOptions()
        {
            RandomizerOptions opt = new RandomizerOptions(FromGame.ER);
            // Item
            opt["item"] = Item;
            opt.Difficulty = Bias;
            opt["raceloc_altboss"] = Raceloc_altboss;
            opt["raceloc_health"] = Raceloc_health;
            opt["raceloc_shops"] = Raceloc_shops;
            opt["raceloc_scadu"] = Raceloc_scadu;
            opt["nocaves"] = Nocaves;
            opt["racemode_key"] = Racemode_key;
            opt["norandom"] = Norandom;
            opt["racemode_health"] = Racemode_health;
            opt["norandom_health"] = Norandom_health;
            opt["racemode_upgrades"] = Racemode_upgrades;
            opt["norandom_upgrades"] = Norandom_upgrades;
            opt["racemode_scadu"] = Racemode_scadu;
            opt["norandom_scadu"] = Norandom_scadu;
            opt["spellshops"] = Spellshops;
            opt["dlcblessing"] = Dlcblessing;
            opt["weaponprogression"] = Weaponprogression;
            opt["mats"] = Mats;
            opt["copydrops"] = Copydrops;
            opt["customitem"] = Customitem;
            // Logic
            opt.SetNonDefaultInt(RandomizerOptions.IntOpt.RunesEnd, Runes_end);
            opt.SetNonDefaultInt(RandomizerOptions.IntOpt.RunesLeyndell, Runes_leyndell);
            if (Runes_rold_default)
            {
                opt.SetInt(RandomizerOptions.IntOpt.RunesRold, Runes_rold);
            }
            opt["rustykey"] = Rustykey;
            opt["liurniagate"] = Liurniagate;
            opt["latedragonbarrow"] = Latedragonbarrow;
            opt["altusgate"] = Altusgate;
            opt["royalaccess"] = Royalaccess;
            opt["deathless"] = Deathless;
            opt["markareas"] = Markareas;
            opt["markitems"] = Markitems;
            opt["earlylegacy"] = Earlylegacy;
            opt["earlymedal"] = Earlymedal;
            opt["moremaxslots"] = Moremaxslots;
            opt["dlckeysilo"] = Dlckeysilo;
            opt["gaolkeys"] = Gaolkeys;
            if (Messmershard_default)
            {
                opt.SetInt(RandomizerOptions.IntOpt.Messmershard, Messmershard);
                opt.SetInt(RandomizerOptions.IntOpt.Shardreq, Shardreq);
            }
            opt["enemy"] = Enemy;
            opt["scale"] = Scale;
            opt["phasehp"] = Phasehp;
            opt["editnames"] = Editnames;
            opt["bossbgm"] = Bossbgm;
            opt["swaprewards"] = Swaprewards;
            opt["swapboss"] = Swapboss;
            opt["nightday"] = Nightday;
            opt["ignoresize"] = Ignoresize;
            opt["customenemy"] = Customenemy;
            // DLC
            opt["dlc"] = Dlc;
            opt["tarnished"] = Tarnished;
            opt["dlcsilo"] = Dlcsilo;
            opt["maxweapon"] = Maxweapon;
            opt["woodsfast"] = Woodsfast;
            opt["nerflantern"] = Nerflantern;
            opt["springseals"] = Springseals;
            opt["dlcstart"] = Dlcstart;
            opt["quickstart"] = Quickstart;
            opt["dlcshop"] = Dlcshop;
            opt["dlcpackage"] = Dlcpackage;
            opt["packagelite"] = Packagelite;
            opt["dlcroundtable"] = Dlcroundtable;
            opt.SetNonDefaultInt(RandomizerOptions.IntOpt.PlayerLevel, Playerlevel);
            opt["maxflasknum"] = Maxflasknum;
            opt["maxflask"] = Maxflask;
            opt["maxpouch"] = Maxpouch;
            opt["maxmemslot"] = Maxmemslot;
            opt["maxwhetblade"] = Maxwhetblade;
            opt["maxbb"] = Maxbb;
            // Misc
            opt["invertstarting"] = Invertstarting;
            opt["onehand"] = Onehand;
            opt["nohand"] = Nohand;
            opt["changestats"] = Changestats;
            opt["dlconlyhand"] = Dlconlyhand;
            opt["invertgifts"] = Invertgifts;
            opt["invertoutfits"] = Invertoutfits;
            opt["invertenvbgm"] = Invertenvbgm;
            opt["invertgestures"] = Invertgestures;
            opt["rainbow"] = Rainbow;
            opt["weaponreqs"] = Weaponreqs;
            opt["fog"] = Fog;
            opt["crawl"] = Crawl;
            opt["nerfsh"] = Nerfsh;
            opt["randomania"] = Randomania;
            opt["allmaps"] = Allmaps;
            opt["allcraft"] = Allcraft;
            opt["nerfmalenia"] = Nerfmalenia;
            opt["nerfgargoyles"] = Nerfgargoyles;
            opt["sombermode"] = Sombermode;
            opt["snowfast"] = Snowfast;
            opt["crashfix"] = Crashfix;
            opt["helper"] = Helper;
            opt["uxm"] = Uxm;
            opt["hideseed"] = Hideseed;
            return opt;
        }

        [Localize]
        private static readonly Text presetFileError = new Text(
            "Error: failed to load file required for customization",
            "EldenForm_presetFileError");

        private async void SetEnemyPreset(EnemyPreset? preset, bool createIfAbsent = false)
        {
            if (preset == null && selectedEnemyPreset == null && !createIfAbsent)
            {
                // Nothing to do here, aside from initializing EnemyPresetText
                UpdateEnemyPresetText();
            }
            if (preset != null || createIfAbsent)
            {
                if (!LoadEnemyAnnotations(out string? err))
                {
                    await Modal(MessageBoxManager.GetMessageBoxStandard(
                        messages.Get(presetFileError),
                        err,
                        ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    return;
                }
            }
            if (preset == null && createIfAbsent)
            {
                // Migration from using Custom.txt. First check CustomEnemy.txt, then copy either from name from options or Custom.txt
                if (!File.Exists(EnemyPreset.DefaultFileName))
                {
                    if ((legacyEnemyPresetName != null && EnemyPreset.GetEnemyPresetPath(legacyEnemyPresetName, out string copyPath))
                        || EnemyPreset.GetEnemyPresetPath("Custom", out copyPath))
                    {
                        File.Copy(copyPath, EnemyPreset.DefaultFileName);
                    }
                }
                try
                {
                    preset = EnemyPreset.LoadPreset(EnemyPreset.DefaultName);
                }
                catch (Exception ex)
                {
#if DEBUG
                    // May not exist, that's fine
                    Console.WriteLine(ex);
#endif
                }
                if (preset == null)
                {
                    EnemyPreset defaultPreset = EnemyPreset.MakeClassBasedDefault(enemyAnn);
                    try
                    {
                        defaultPreset.SavePreset();
                        preset = defaultPreset;
                    }
                    catch (Exception ex)
                    {
                        await Modal(MessageBoxManager.GetMessageBoxStandard(
                            messages.Get(presetFileError),
                            messages.Get(MiscSetup.CreateFileError, ex.Message),
                            ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    }
                }
            }
            selectedEnemyPreset = preset;
            if (selectedEnemyPreset != null)
            {
                // At this point, preset and enemyAnn both exist. This isn't true of all LoadPreset invocations, so migrate it here.
                selectedEnemyPreset.MigrateVersion(enemyAnn);
            }
            UpdateEnemyPresetText();
        }

        private void UpdateEnemyPresetText()
        {
            if (enemyAnn != null && selectedEnemyPreset != null)
            {
                EnemyPresetText = selectedEnemyPreset.FullDescription(messages, enemyAnn, Dlcsilo);
            }
            else
            {
                EnemyPresetText = DefaultEnemyPresetDesc();
            }
        }

        // Maybe temporary to allow for fully localized enemy desc without any extra config files, though .enemy.json is necessary for the full thing.
        [Localize]
        private static readonly Text agingUntouchableText = new Text("Aging Untouchable", "EldenForm_nerflanternEnemy");

        private string DefaultEnemyPresetDesc()
        {
            return EnemyPreset.DefaultDescription(
                messages,
                new()
                {
                    EnemyAnnotations.EnemyClass.Basic, EnemyAnnotations.EnemyClass.Boss, EnemyAnnotations.EnemyClass.MinorBoss,
                    EnemyAnnotations.EnemyClass.Miniboss, EnemyAnnotations.EnemyClass.NightMiniboss, EnemyAnnotations.EnemyClass.DragonMiniboss,
                    EnemyAnnotations.EnemyClass.Evergaol, EnemyAnnotations.EnemyClass.Wildlife, EnemyAnnotations.EnemyClass.HostileNPC,
                },
                new() { EnemyAnnotations.EnemyClass.Scarab },
                Dlcsilo,
                messages.Get(agingUntouchableText));
        }

        [Localize]
        private static readonly Text allitem0 = new Text(
            "All eligible locations for items are equally likely.",
            "EldenForm_allitem0");
        [Localize]
        private static readonly Text allitem1 = new Text(
            "Most eligible locations for items are equally likely.",
            "EldenForm_allitem1");
        [Localize]
        private static readonly Text allitem2 = new Text(
            "Better rewards for difficult and late locations.",
            "EldenForm_allitem2");
        [Localize]
        private static readonly Text allitem3 = new Text(
            "Much better rewards for difficult and late locations.",
            "EldenForm_allitem3");
        [Localize]
        private static readonly Text keyitem0 = new Text(
            "Key items may be in locations that don't require completing much of the game.",
            "EldenForm_keyitem0");
        [Localize]
        private static readonly Text keyitem1 = new Text(
            "Key items will usually be in different areas and depend on each other.",
            "EldenForm_keyitem1");
        [Localize]
        private static readonly Text keyitem2 = new Text(
            "Key items will usually be in different areas and form interesting chains.",
            "EldenForm_keyitem2");
        [Localize]
        private static readonly Text keyitem3 = new Text(
            "Key items will usually form long chains across different areas.",
            "EldenForm_keyitem3");
        [Localize]
        private static readonly Text checkAmount = new Text(
            "{0} checks",
            "EldenForm_checkAmount");
        [Localize]
        private static readonly Text importantExcluded = new Text(
            "Excluded by current options:",
            "EldenForm_checkImportantExcluded");

        private void UpdateBiasText()
        {
            Text loc;
            RandomizerOptions.BiasWeights weights = RandomizerOptions.BiasWeights.Calculate(Bias, FromGame.ER);
            if (weights.SpreadRatio > 0.7) loc = allitem3;
            else if (weights.SpreadRatio > 0.3) loc = allitem2;
            else if (weights.SpreadRatio > 0.001) loc = allitem1;
            else loc = allitem0;
            Text? chain;
            if (weights.KeyItemChain <= 1.1f) chain = keyitem0;
            else if (weights.KeyItemChain <= 4.001) chain = keyitem1;
            else if (weights.KeyItemChain <= 10) chain = keyitem2;
            else chain = keyitem3;
            if (Norandom) chain = null;
            BiasDesc = $"{messages.Get(loc)}\r\n{messages.Get(chain)}";
            BiasLabel = $"{Bias}%";
        }

        // Key is based on the WinForms control name
        private int GetLocationCount(string key)
        {
            if (!AnnotationData.EldenTagCounts.TryGetValue(key, out int[]? counts))
            {
                return 0;
            }
            // none; dlc; minidungeon; dlc,minidungeon
            bool[] values = new[] { true, Dlc, !Nocaves, Dlc && !Nocaves };
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (values[i])
                {
                    count += counts[i];
                }
            }
            return count;
        }

        [ReactiveCommand]
        private async void ShowLocations(string labelName)
        {
            if (!LoadPresetAnnotations(out string? err))
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(presetFileError),
                    err,
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
            List<string> requireTags = AnnotationData.EldenTagOpts.Where(e => e.Value == labelName).Select(e => e.Key).ToList();
            if (requireTags.Count == 0)
            {
                return;
            }
            List<string> excludeTags = new();
            if (!Dlc) excludeTags.Add("dlc");
            if (Nocaves) excludeTags.Add("minidungeon");
            // Gaol keys are still key items, but not randomized by default, so avoid showing them here
            if (!Gaolkeys) excludeTags.Add("gaolkey");
            // This routine is somewhat custom. It could use ItemRepository maybe
            SortedSet<(int, string)> entries = new();
            SortedSet<(int, string)> excludedEntries = new();
            foreach (AnnotationData.PresetTagInfo info in presetAnn!.Locations)
            {
                if (info.Locations == null) continue;
                if (info.Tags.Intersect(requireTags).Any())
                {
                    if (info.Tags.Contains("missable"))
                    {
                        continue;
                    }
                    else if (info.Tags.Intersect(excludeTags).Any())
                    {
                        excludedEntries.UnionWith(info.Locations.Select(e => (e.Value, e.Key + "\n")));
                    }
                    else
                    {
                        entries.UnionWith(info.Locations.Select(e => (e.Value, e.Key + "\n")));
                    }
                }
            }
            string text = string.Join("", entries.Select(e => e.Item2));
            if (excludedEntries.Count > 0)
            {
                string sep = string.IsNullOrEmpty(text) ? "" : "\n";
                text = text + sep + messages.Get(importantExcluded) + "\n" + string.Join("", excludedEntries.Select(e => e.Item2));
            }
            // This does not have search functionality like EldenForm
            string? getTagText(string tag) => ItemPreset.LocationGroupNames.TryGetValue(tag, out Text? tagText) ? messages.Get(tagText) : null;
            string title = string.Join(", ", requireTags.Select(getTagText).Where(t => t != null));
            await Modal(MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = title,
                ContentMessage = text,
                ButtonDefinitions = ButtonEnum.Ok,
                EscDefaultButton = ClickEnum.Ok,
                EnterDefaultButton = ClickEnum.Ok,
                MaxHeight = 500,
                MinWidth = 400,
                MaxWidth = 1000,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }));
        }

        // Always call UpdateEnabled after this
        private async void SetItemPreset(ItemPreset? itemPreset, bool createIfAbsent = false, bool promptUpdate = false)
        {
            // Nothing to do here, probably? But we need state update below to set checkbox/settings/etc
            // if (itemPreset == null && selectedItemPreset == null && !createIfAbsent) return;
            if (itemPreset != null || createIfAbsent)
            {
                if (!LoadItemAnnotations(out string? err) || !LoadPresetAnnotations(out err))
                {
                    await Modal(MessageBoxManager.GetMessageBoxStandard(
                        messages.Get(presetFileError),
                        err,
                        ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    return;
                }
            }
            bool created = false;
            if (itemPreset == null && createIfAbsent)
            {
                try
                {
                    itemPreset = ItemPreset.LoadPreset();
                }
                catch (Exception ex)
                {
#if DEBUG
                    // May not exist, that's fine
                    Console.WriteLine("Ignoring error: " + ex);
#endif
                }
                if (itemPreset == null)
                {
                    try
                    {
                        ItemPreset defaultPreset = itemAnn.DefaultPreset.ProcessBuiltins(MakeDefaultOptions());
                        defaultPreset.SavePreset();
                        itemPreset = defaultPreset;
                        created = true;
                    }
                    catch (Exception ex)
                    {
                        await Modal(MessageBoxManager.GetMessageBoxStandard(
                            messages.Get(presetFileError),
                            messages.Get(MiscSetup.CreateFileError, ex.Message),
                            ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    }
                }
            }
            if (itemPreset == null)
            {
                ItemPresetViewModel = null;
                ItemPresetText = "";
            }
            else
            {
                // TODO: Don't reconstruct this
                ItemRepository repo = new(itemAnn, presetAnn);
                // Validation/simple migration flow. This probably shouldn't do anything for newly created presets.
                ItemPreset updated = itemPreset.UpdateFromOptions(MakeOptions(), itemAnn.DefaultPreset, false, null);
                if (updated != null)
                {
                    itemPreset = updated;
                }
                ItemPresetViewModel = new(repo, itemPreset, this);
                ItemPresetText = itemPreset.FullDescription(messages, presetAnn.GetItemName);
            }
        }
        
        private Color CalculateSeedColor(uint val)
        {
            if (val == 0)
            {
                return SystemColors.Window;
            }
            uint hash = Util.FNV1AHash(val);
            double hue = ((double)hash / uint.MaxValue) * 360;
            double sat = 0.08 + 0.08 * ((byte)hash) / 255.0;
            double value = 1 - 0.1 * ((byte)(hash >> 8)) / 255.0;
            return Util.ColorFromHSV(hue, sat, value);
        }

        private RandomizerOptions OptionsWithSeed()
        {
            // For now, keep this separate from the main _options so that seed doesn't have to feed from the previous one in a weird way.
            RandomizerOptions opt = Options.Copy();
            opt.Seed = RunSeed;
            opt.Seed2 = RunSeed2;
            return opt;
        }

        [Localize]
        private static readonly Text seedError = new Text(
            "Invalid fixed seed",
            "EldenForm_seedError");
        [Localize]
        private static readonly Text enemyseedError = new Text(
            "Invalid enemy seed",
            "EldenForm_enemyseedError");
        [Localize]
        private static readonly Text runningText = new Text(
            "Running...",
            "EldenForm_running");
        [Localize]
        private static readonly Text runSuccessStatus = new Text(
            "Done! Hints and spoilers in spoiler_logs directory as {0} - Restart your game!!",
            "EldenForm_runSuccess");
        [Localize]
        private static readonly Text runError = new Text(
            "Error encountered: {0}\nIt may work to try again with a different seed. See most recent file in spoiler_logs directory for the full error.",
            "EldenForm_runError");
        [Localize]
        private static readonly Text runExtractWarningTitle = new Text(
            "Unexpected game files",
            "EldenForm_runExtractWarningTitle");
        [Localize]
        private static readonly Text runExtractWarning = new Text(
            "Randomization succeeded, but the following files extracted from the game directory have unexpected contents. This version of the randomizer might be incompatible with the installed game version and might not function correctly.",
            "EldenForm_runExtractWarning");
        [Localize]
        private static readonly Text runErrorGeneric = new Text(
            "Error encountered",
            "EldenForm_runErrorGeneric");
        [Localize]
        private static readonly Text runErrorStatus = new Text(
            "Error! Partial log in spoiler_logs directory as {0}",
            "EldenForm_runErrorStatus");

        [ReactiveCommand]
        public async void Randomize()
        {
            if (Working) return;
            if (!DoBasicCheck(out string err))
            {
                SetError(err);
                SetStatus(messages.Get(runErrorGeneric), failure: true);
                return;
            }
            bool newFixedSeed = false;
            bool newEnemySeed = false;
            Random seedRandom = new Random();
            if (!RerollSeed && !string.IsNullOrWhiteSpace(Seed))
            {
                if (uint.TryParse(Seed.Trim(), out uint seed) && seed != 0)
                {
                    RunSeed = seed;
                }
                else
                {
                    SetError(messages.Get(seedError));
                    SetStatus(messages.Get(runErrorGeneric), failure: true);
                    return;
                }
            }
            else
            {
                RunSeed = (uint)seedRandom.Next();
                newFixedSeed = true;
            }
            // TODO: Translate state defaultRerollEnemy.Enabled
            if (!RerollSeed2 && !string.IsNullOrWhiteSpace(Seed2))
            {
                if (uint.TryParse(Seed2.Trim(), out uint seed) && seed != 0)
                {
                    RunSeed2 = seed;
                }
                else
                {
                    SetError(messages.Get(enemyseedError));
                    SetStatus(messages.Get(runErrorGeneric), failure: true);
                    return;
                }
            }
            else if (RerollSeed2)
            {
                RunSeed2 = (uint)seedRandom.Next();
                newFixedSeed = true;
            }
            else
            {
                RunSeed2 = 0;
            }
            if (!MiscSetup.ModifyEldenRingFiles(messages, Exe, out string errorText))
            {
                SetError(errorText);
                SetStatus(messages.Get(runErrorGeneric), true);
                return;
            }
            RandomizerOptions rand = OptionsWithSeed();
            RandomizerOptions.SaveOptions(rand);
            Working = true;
            SetError();
            Seed = RunSeed.ToString();
            Seed2 = RunSeed2 == 0 ? "" : RunSeed2.ToString();
            runner.DeleteLaunchFile();
            // This is no longer used anymore
            if (File.Exists("config_eldenringrandomizer_dll.toml"))
            {
                File.Delete("config_eldenringrandomizer_dll.toml");
            }
            UpdateCanLaunch();
            bool useLaunchFile = !Uxm;

            Randomizer randomizer = new Randomizer();
            (string? displayFile, string? error) = await Task.Factory.StartNew<(string?, string?)>(() =>
            {
                // Actual randomization
                string seed2 = rand.Seed2 == 0 || rand.Seed2 == rand.Seed ? "" : $"_{rand.Seed2}";
                string seedStr = $"{rand.Seed}{seed2}";
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
                string hashStr = rand.ConfigHash();
                string runId = $"{timeStr}_log_{seedStr}_{hashStr}.txt";
                string displayFile = runId;
                if (Hideseed)
                {
                    displayFile = $"{timeStr}_log_{Regex.Replace(seedStr, @"\d", "?")}_{hashStr}.txt";
                }
                TextWriter log;
                try
                {
                    Directory.CreateDirectory("spoiler_logs");
                    log = File.CreateText($@"spoiler_logs\{runId}");
                }
                catch (Exception ex)
                {
                    return (null, messages.Get(MiscSetup.CreateFileError, ex.Message));
                }
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    MergedMods mods = MergedMods.FromPath(Mod);
                    randomizer.Randomize(
                        rand, FromGame.ER, status => Dispatcher.UIThread.Post(() => SetStatus(status)),
                        enemyPreset: selectedEnemyPreset, itemPreset: ItemPresetViewModel?.Preset,
                        messages: messages, gameExe: Exe, modDirs: mods);
                    if (useLaunchFile)
                    {
                        CreateLaunchFile(rand, mods);
                    }
                    return (displayFile, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return (displayFile, messages.Get(runError, ex.Message));
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });
            if (displayFile == null)
            {
                SetStatus(messages.Get(runErrorGeneric), true);
            }
            else
            {
                if (error == null)
                {
                    SetStatus(messages.Get(runSuccessStatus, displayFile), success: true);
                }
                else
                {
                    SetStatus(messages.Get(runErrorStatus, displayFile), failure: true);
                }
            }
            if (error == null)
            {
                // Final warning thanks to global static state. Also use local state to avoid spamming it
                IReadOnlyList<string> mismatch = GameData.GetMismatchedFiles();
                if (mismatch != null && mismatch.Count > 0)
                {
                    string sep = showedMismatchWarning ? " " : Environment.NewLine;
                    string mismatchError = messages.Get(runExtractWarning) + sep + sep + string.Join(sep, mismatch);
                    if (showedMismatchWarning)
                    {
                        error = mismatchError;
                    }
                    else
                    {
                        await Modal(MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                        {
                            ContentTitle = messages.Get(runExtractWarningTitle),
                            ContentMessage = mismatchError,
                            ButtonDefinitions = ButtonEnum.Ok,
                            Icon = Icon.Warning,
                            MaxHeight = 500,
                            MaxWidth = 800,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        }));
                        showedMismatchWarning = true;
                    }
                }
            }
            SetError(error);
            UpdateCanLaunch();
            Working = false;
            Collect = true;
        }
        
        private void SetStatus(string status, bool success = false, bool failure = false)
        {
            Status = status;
            StatusSuccess = success;
            StatusFailure = failure;
        }

        [Localize]
        private static readonly Text status = new Text(
            "{0} Current config hash: {1}",
            "EldenForm_status");
        [Localize]
        private static readonly Text credits = new Text(
            "Created by {0}.",
            "EldenForm_status_credits");
        [Localize]
        private static readonly Text creditsWithTranslators = new Text(
            "Created by {0}, translated by {1}.",
            "EldenForm_status_creditsWithTranslators");
        
        private string GetDefaultStatus(RandomizerOptions options)
        {
            string cred;
            if (string.IsNullOrWhiteSpace(Translators))
            {
                cred = messages.Get(credits, "thefifthmatt");
            }
            else
            {
                cred = messages.Get(creditsWithTranslators, "thefifthmatt", Translators);
            }
            return messages.Get(status, cred, options?.ConfigHash() ?? "00000");
        }

        [Localize]
        private static readonly Text overviewText = new Text(
            "Check the mod page or README for a full list of all key items, important locations, and other details. Find hints and spoilers for item locations in the spoiler_logs directory.",
            "EldenForm_overviewL");
        private void SetError(string? err = null)
        {
            Message = err ?? messages.Get(overviewText);
            MessageError = err != null;
        }

        [Localize]
        private static readonly Text randomizeRun = new Text(
            "Run",
            "EldenForm_randomizeRun");
        [Localize]
        private static readonly Text randomizeBoth = new Text(
            "Randomize items and enemies",
            "EldenForm_randomizeBoth");
        [Localize]
        private static readonly Text randomizeItem = new Text(
            "Randomize items",
            "EldenForm_randomizeItem");
        [Localize]
        private static readonly Text randomizeSame = new Text(
            "Run with set seed",
            "EldenForm_randomizedFixed");
        [Localize]
        private static readonly Text randomizeEnemy = new Text(
            "Randomize enemies",
            "EldenForm_randomizeEnemy");
        [Localize]
        private static readonly Text randomizeItemOnly = new Text(
            "Reroll items (same enemies)",
            "EldenForm_randomizeItemOnly");
        [Localize]
        private static readonly Text randomizeEnemyOnly = new Text(
            "Reroll enemies (same items)",
            "EldenForm_randomizeEnemyOnly");

        private Text GetRandomizeButtonText()
        {
            Text text;
            if (Working)
            {
                return runningText;
            }
            if (Enemy && Item)
            {
                text = randomizeBoth;
                if (RerollSeed)
                {
                    if (!RerollSeed2 && uint.TryParse(Seed2.Trim(), out _))
                    {
                        text = randomizeItemOnly;
                    }
                }
                else
                {
                    if (RerollSeed2)
                    {
                        text = randomizeEnemyOnly;
                    }
                    else
                    {
                        text = randomizeSame;
                    }
                }
            }
            else if (Enemy)
            {
                text = randomizeEnemy;
            }
            else if (Item)
            {
                text = randomizeItem;
            }
            else
            {
                text = randomizeRun;
            }
            return text;
        }

        private void SetCulture(CultureInfo culture)
        {
            // TODO:
            // Language textbox
            // Changing language
            // Changing font
        }

        // Maybe add this
        [Localize]
        private static readonly Text launchMismatch = new Text(
            "Randomizer options appear to be changed from the last time randomizer was run. Launch anyway?", "EldenForm_launchMismatch");
        [Localize]
        private static readonly Text launchMismatchZip = new Text(
            "Randomizer options appear to be changed from the last time randomizer was run. Zip anyway?", "EldenForm_launchMismatchZip");

        [Localize]
        private static readonly Text launchRunning = new Text(
            "Elden Ring appears to be already running, so launching with Mod Engine will have no effect. Try anyway?", "EldenForm_launchRunning");
        [Localize]
        private static readonly Text launchFailedTitle = new Text(
            "Launch failed", "EldenForm_launchFailedTitle");
        [Localize]
        private static readonly Text launchFog = new Text(
            "If you're using Fog Gate Randomizer (under Misc Options), launch the game from FogMod.exe, not from here", "EldenForm_launchFog");
        [Localize]
        private static readonly Text launchFailed = new Text(
            "Automatic Mod Engine launcher appeared to fail.\n\nMake sure Steam is running. If the game is currently running, close it and wait until it is fully shut down.\n\nCheck Troubleshooting on https://www.nexusmods.com/eldenring/mods/428/ to see when Mod Engine might fail. You may need to try using a manual Mod Engine launcher or Nuxe installation.", "EldenForm_launchFailed");

        private void CreateLaunchFile(RandomizerOptions options, MergedMods mods)
        {
            StringWriter writer = new StringWriter();
            RandomizerOptionsFile.Create(Randomizer.EldenVersion, options).Save(writer);
            string hash = Util.GetMD5TextHash(writer.ToString());
            List<string> comments = new List<string>
            {
                $"Produced by Elden Ring Randomizer {Randomizer.EldenVersion}",
                $"<hash>{hash}</hash>",
            };
            runner.CreateLaunchFile(comments, MiscSetup.GetModEngineOutputDirectory(), mods, Dlls.ToList());
        }

        [ReactiveCommand(CanExecute = nameof(launchEnabled))]
        public async void LaunchGame()
        {
            UpdateCanLaunch();
            if (!CanLaunch || runner.IsLaunching())
            {
                return;
            }
            if (Fog)
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(launchFailedTitle), messages.Get(launchFog),
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
            if (runner.IsGameRunning())
            {
                ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ConfirmTitleText), messages.Get(launchRunning),
                    ButtonEnum.OkCancel, Icon.Question, WindowStartupLocation.CenterOwner));
                if (result != ButtonResult.Ok)
                {
                    return;
                }
            }
            await runner.LaunchGame();
        }

        private void UpdateCanLaunch()
        {
            // Allow doing this on-demand, as it can become valid based on filesystem state
            CanLaunch = runner.IsValid() && !Uxm;
        }

        [Localize]
        private static readonly Text zipMergeConfirm = new Text(
            "Include the following merged mod directories in the zip file?", "EldenForm_zipMergeConfirm");
        [Localize]
        private static readonly Text zipStatusProgress = new Text(
            "Zipping mod files...", "EldenForm_zipStatusProgress");
        [Localize]
        private static readonly Text zipStatusDone = new Text(
            "Zipped {0}", "EldenForm_zipStatusDone");
        private static readonly FilePickerFileType ZipFileType = new("Zip file") { Patterns = ["*.zip"] };

        [ReactiveCommand]
        public async void Zip()
        {
            // This is a lot of logic but most of it is interactive
            UpdateCanLaunch();
            if (!CanLaunch) return;
            // One more heuristic check before the zip gets generated
            if (!File.Exists("regulation.bin")) return;

            if (MainWindow()?.StorageProvider is not IStorageProvider storageProvider)
            {
                return;
            }

            MergedMods mods;
            try
            {
                mods = MergedMods.FromPath(Mod);
            }
            catch (Exception ex)
            {
                // Mainly TOML parse errors - probably fine to ignore, but just handle
                SetError(ex.Message);
                SetStatus(messages.Get(runErrorGeneric), true);
                return;
            }
            if (mods.Count > 0)
            {
                // Include the following merged mod directories in the zip file?
                ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ConfirmTitleText),
                    messages.Get(zipMergeConfirm) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, mods.Dirs),
                    ButtonEnum.YesNoCancel, Icon.Question, WindowStartupLocation.CenterOwner));
                if (result == ButtonResult.No)
                {
                    // Forget about them for now, rather than including nonexistent directories in the toml
                    mods = new MergedMods();
                }
                else if (result != ButtonResult.Yes)
                {
                    return;
                }
            }
            foreach (string dir in mods.Dirs)
            {
                // Allow this if there's a UXM mod installed, but not here. Don't localize this for now (?)
                if (File.Exists(Path.Combine(dir, "eldenring.exe")))
                {
                    SetError("Merged mod directory appears to be the game directory. You shouldn't include this in the zip. Clear your merged mod directory first.");
                    SetStatus(messages.Get(runErrorGeneric), true);
                    return;
                }
            }

            string seedStr = "";
            if (RunSeed != 0 && !Hideseed)
            {
                seedStr += "_" + RunSeed;
                if (RunSeed2 != 0 && RunSeed2 != RunSeed)
                {
                    seedStr += "_" + RunSeed2;
                }
            }
            IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                SuggestedFileName = $"EldenRingRandomizer{seedStr}_{MakeOptions().ConfigHash()}.zip",
                FileTypeChoices = [ZipFileType, FilePickerFileTypes.All],
                ShowOverwritePrompt = true,
            });
            if (file?.TryGetLocalPath() is not string path)
            {
                return;
            }

            List<string> modFiles = MiscSetup.GetGameFiles(Directory.GetCurrentDirectory(), false);
            // Also add dlls, it's probably fine to not filter those by hash, as dlls in other mods will also get added
            string dllDir = Path.Combine(Directory.GetCurrentDirectory(), "dll");
            if (Directory.Exists(dllDir))
            {
                foreach (string dllPath in Directory.GetFiles(dllDir))
                {
                    if (dllPath.EndsWith(".dll") || dllPath.EndsWith("_config.ini"))
                    {
                        modFiles.Add(dllPath);
                    }
                }
            }
            Dictionary<string, List<string>> explicitFiles = new() { [""] = modFiles };
            SetStatus(messages.Get(zipStatusProgress));
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    runner.ZipModEngineDir("rando", path, mods, Dlls, explicitFiles);
                });
                // Not sure what the point of this is if the default file name has the seed
                SetStatus(messages.Get(zipStatusDone, Path.GetFileName(path)), success: true);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                SetStatus(messages.Get(runErrorGeneric), true);
            }
        }

        private IObservable<bool> canHelper => this.WhenAnyValue(x => x.Helper, x => (bool)x);
        [ReactiveCommand(CanExecute = nameof(canHelper))]
        public async void EditHelper()
        {
            if (!File.Exists(HelperConfigPath))
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ErrorTitleText), messages.Get(FormText.FileMissingText, HelperConfigPath),
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
            await ShowHelperWindow.Handle(Unit.Default);
        }

        private IObservable<bool> canCustomEnemy => this.WhenAnyValue(x => x.Customenemy, x => (bool)x);
        [ReactiveCommand(CanExecute = nameof(canCustomEnemy))]
        public async void EditCustomEnemy()
        {
            if (selectedEnemyPreset == null || enemyAnn == null) return;
            EnemyRepository repo = new(enemyAnn);
            EnemyPresetViewModel viewModel = new(repo, selectedEnemyPreset, this);
            EnemyPreset savedPreset = await ShowEnemyPreset.Handle(viewModel);
            if (savedPreset != null)
            {
                SetEnemyPreset(savedPreset);
            }
            return;
#if WINFORMS
            using (PresetEditForm presetForm = new PresetEditForm(enemyAnn, messages, selectedEnemyPreset, Dlcsilo))
            {
                ShowWinFormsDialog(presetForm);
                if (presetForm.SavedPreset != null)
                {
                    SetEnemyPreset(presetForm.SavedPreset, false);
                }
            }
#endif
        }

#if DEBUG && WINFORMS
        internal PresetEditForm GetDebugEnemyPresetForm()
        {
            if (!LoadEnemyAnnotations(out _)) throw new Exception("No enemy annotations");
            EnemyPreset defaultPreset = EnemyPreset.MakeClassBasedDefault(enemyAnn);
            return new PresetEditForm(enemyAnn, messages, defaultPreset, Dlcsilo);
        }
#endif

        private static bool IsValidExe(string path) => File.Exists(path) && Path.GetFileName(path).Equals(ExeName, StringComparison.OrdinalIgnoreCase);
        private static readonly FilePickerFileType ExeType = new("Elden Ring") { Patterns = [ExeName] };
        [ReactiveCommand]
        public async void SelectExe()
        {
            if (MainWindow() is Window window)
            {
                IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = $"Select {ExeName}",
                    FileTypeFilter = [ExeType, FilePickerFileTypes.All],
                    AllowMultiple = false,
                });
                if (files.Count > 0 && files[0].TryGetLocalPath() is string path && IsValidExe(path))
                {
                    Exe = path;
                }
            }
        }

        [ReactiveCommand]
        public async void MergeMods()
        {
            string? result = await ShowMergeModDialog.Handle(
                new MergeModViewModel(MergeModViewModel.MergeType.Regulation, MergeModViewModel.MergeType.Directory, MergeModViewModel.MergeType.ModEngine2));
            if (result != null)
            {
                Mod = result;
            }
        }

        [Localize]
        private static readonly Text DllInfoText = new Text(
            "These dll mods are automatically loaded when \"Launch Elden Ring\" is used. They must implement DirectInput8Create and not require any initialization delay. To load dlls with manual Mod Engine, instead add them to external_dlls in the toml file.",
            "DllForm_infoL");

        [ReactiveCommand]
        public async void AddDll()
        {
            ImmutableList<string> prevDlls = Dlls;
            DllViewModel dllModel = new DllViewModel(Dlls) { LaunchText = DllInfoText };
            await ShowDllDialog.Handle(dllModel);
            if (dllModel.ResultDlls != null)
            {
                Dlls = dllModel.ResultDlls;
                SyncDllOptions(true);
                if (!Dlls.SequenceEqual(prevDlls))
                {
                    runner?.DeleteLaunchFile();
                    UpdateCanLaunch();
                }
            }
        }

        private void SyncDllOptions(bool dllsAuthoritative)
        {
            bool helperPresent = Dlls.Any(f => f.Equals(helperPath, StringComparison.OrdinalIgnoreCase));
            bool crashfixPresent = Dlls.Any(f => f.Equals(crashfixPath, StringComparison.OrdinalIgnoreCase));
            if (dllsAuthoritative)
            {
                Helper = helperPresent;
                Crashfix = crashfixPresent;
            }
            else
            {
                // Option authoritative
                ImmutableList<string> newDlls = Dlls;
                if (helperPresent && !Helper)
                {
                    newDlls = newDlls.RemoveAll(f => f.Equals(helperPath, StringComparison.OrdinalIgnoreCase));
                }
                else if (!helperPresent && Helper)
                {
                    newDlls = newDlls.Add(helperPath);
                }
                if (crashfixPresent && !Crashfix)
                {
                    newDlls = newDlls.RemoveAll(f => f.Equals(crashfixPath, StringComparison.OrdinalIgnoreCase));
                }
                else if (!crashfixPresent && Crashfix)
                {
                    newDlls = newDlls.Add(crashfixPath);
                }
                Dlls = newDlls;
            }
        }

        [ReactiveCommand]
        public void OpenCurrentDirectory()
        {
            DirectoryInfo dir = new(MiscSetup.GetModEngineOutputDirectory());
            if (dir.Exists)
            {
                Process.Start(new ProcessStartInfo(dir.FullName) { UseShellExecute = true });
            }
        }

        [ReactiveCommand]
        public void CheckUpdates()
        {
            Process.Start(new ProcessStartInfo("https://www.nexusmods.com/eldenring/mods/428?tab=files") { UseShellExecute = true });
        }

        [ReactiveCommand]
        public void ToggleDarkMode()
        {
            // Why not
            if (Application.Current!.ActualThemeVariant == ThemeVariant.Light)
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            }
            else
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            }
        }

        [ReactiveCommand]
        public async void ShowOptions()
        {
            string str = OptionsWithSeed().FullString();
            OptionsViewModel.Result result = await ShowOptionsDialog.Handle(new OptionsViewModel { Text = str });
            if (result == null)
            {
                return;
            }
            if (result.Error != null)
            {
                SetError(result.Error);
                return;
            }
            else if (result.Options != null)
            {
                SetOptionsFromString(result.Options, true);
            }
            else
            {
                SetOptionsFromString(DefaultOpts, true);
            }
            // Do this here again, especially in case RunSeed changed but options didn't
            RandomizerOptions saved = OptionsWithSeed();
            RandomizerOptions.SaveOptions(saved);
        }

        [ReactiveCommand]
        public async void ResetOptions()
        {
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                "Confirm",
                "Reset all options to default?",
                ButtonEnum.OkCancel, Icon.Question, WindowStartupLocation.CenterOwner));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            // I guess also reset this, it's outside of normal options
            Mod = "";
            Dlls.Clear();
            SetOptionsFromString(DefaultOpts, true);
            // Just in case, make sure necessary dlls are present
            SyncDllOptions(false);
        }

        [Localize]
        private static readonly Text dllList = new Text(
            "Using {0}",
            "EldenForm_dllList");
        [Localize]
        private static readonly Text restoreTitle = new Text(
            "Restore files",
            "EldenForm_restoreAlertTitle");
        [Localize]
        private static readonly Text restoreErrorBadExe = new Text(
            "Game exe not found",
            "EldenForm_restoreErrorBadExe");
        [Localize]
        private static readonly Text restoreNothingToDo = new Text(
            "No .randobak files found in {0}",
            "EldenForm_restoreNothingToDo");
        [Localize]
        private static readonly Text restoreConfirmTitle = new Text(
            "Restore from these files?",
            "EldenForm_restoreConfirmTitle");
        [Localize]
        private static readonly Text restoreFileSuccess = new Text(
            "Restored from {0}",
            "EldenForm_restoreFileSuccess");
        [Localize]
        private static readonly Text restoreFileFailure = new Text(
            "Failed to restore from {0}!",
            "EldenForm_restoreFileFailure");


        [ReactiveCommand]
        public async void Uninstall()
        {
            string title = messages.Get(restoreTitle);
            if (!File.Exists(Exe))
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    title, messages.Get(restoreErrorBadExe), ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
            string gamePath = Path.GetDirectoryName(Exe)!;
            List<string> restoreFiles = GameData.GetBackupFiles(gamePath, FromGame.ER);
            if (restoreFiles.Count == 0)
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    title, messages.Get(restoreNothingToDo, gamePath),
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = messages.Get(restoreConfirmTitle),
                ContentMessage = string.Join(Environment.NewLine, restoreFiles),
                ButtonDefinitions = ButtonEnum.OkCancel,
                MaxHeight = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            List<string> restoreLog = new List<string>();
            foreach (string restoreFile in restoreFiles)
            {
                try
                {
                    GameData.RestoreBackup(restoreFile);
                    restoreLog.Add(messages.Get(restoreFileSuccess, restoreFile));
                }
                catch (Exception ex)
                {
                    restoreLog.Add(messages.Get(restoreFileFailure, restoreFile));
                    restoreLog.Add("");
                    restoreLog.Add(ex.ToString());
                }
            }
            // DS1 randomizer shows more information like counts, this is pretty basic
            await Modal(MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = title,
                ContentMessage = string.Join(Environment.NewLine, restoreLog),
                ButtonDefinitions = ButtonEnum.Ok,
                MaxHeight = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }));
        }

        private static readonly FilePickerFileType RandomizeoptType = new("Randomizer options file") { Patterns = ["*.randomizeopt"] };
        // Probably temporary until this lives in a better place
        private static readonly FilePickerFileType EnemyJsonType = new("Enemy localization files") { Patterns = ["*.enemy.json"] };

        [ReactiveCommand]
        private async void ExportOptions()
        {
            if (MainWindow()?.StorageProvider is not IStorageProvider storageProvider)
            {
                return;
            }
            IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                // Title = $"Select {SaveName} to copy to {targetName}",
                SuggestedFileName = $"{MakeOptions().ConfigHash()}.randomizeopt",
                FileTypeChoices = [RandomizeoptType, FilePickerFileTypes.All],
                ShowOverwritePrompt = true,
            });
            if (file?.TryGetLocalPath() is string path)
            {
                using (TextWriter writer = File.CreateText(path))
                {
                    MakeOptionsFile().Save(writer);
                }
            }
        }

        private RandomizerOptionsFile MakeOptionsFile()
        {
            // I guess use RunSeed still, but with string seeds should probably make it more intuitive
            RandomizerOptions opt = OptionsWithSeed();
            string? helperConfig = null;
            if (Helper && File.Exists(HelperConfigPath))
            {
                helperConfig = File.ReadAllText(HelperConfigPath);
            }
            return RandomizerOptionsFile.Create(
                Randomizer.EldenVersion, opt,
                enemyPreset: selectedEnemyPreset, itemPreset: ItemPresetViewModel?.Preset, helperConfig: helperConfig);
        }

        [Localize]
        private static readonly Text versionMismatch = new Text(
            "Mismatched options versions! File has version {0}, but current randomizer version is {1}. This may result in different output. Try to continue anyway?",
            "EldenForm_versionMismatch");
        [Localize]
        private static readonly Text confirmOverwritePreset = new Text(
            "Overwrite preset file?\n{0}", "EldenForm_confirmOverwritePreset");
        [Localize]
        private static readonly Text confirmCreatePreset = new Text(
            "Create preset file?\n{0}", "EldenForm_confirmCreatePreset");

        [ReactiveCommand]
        private async void ImportOptions()
        {
            if (MainWindow()?.StorageProvider is not IStorageProvider storageProvider)
            {
                return;
            }
            IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                // TODO: Add back EnemyJsonType by recreating the form
                FileTypeFilter = [RandomizeoptType, FilePickerFileTypes.All],
                AllowMultiple = false,
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not string path)
            {
                return;
            }
            // Needs to be ported
#if WINFORMS
            if (path.EndsWith(".enemy.json"))
            {
                ShowBossMessages(path);
                return;
            }
#endif
            RandomizerOptionsFile newOpts = RandomizerOptionsFile.Load(path);
            if (newOpts.Version != Randomizer.EldenVersion)
            {
                ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(FormText.ConfirmTitleText),
                    messages.Get(versionMismatch, newOpts.Version, Randomizer.EldenVersion),
                    ButtonEnum.OkCancel, Icon.Question, WindowStartupLocation.CenterOwner));
                if (result != ButtonResult.Ok)
                {
                    return;
                }
            }
            RandomizerOptions newOpt = newOpts.OptionsValue;
            // For presets, overwrite always (enemy preset used to ask for confirmation)
            EnemyPreset enemyPreset = newOpts.EnemyPresetValue;
            if (enemyPreset != null)
            {
                // Delete existing model first
                Customenemy = false;
                enemyPreset.Name = EnemyPreset.DefaultName;
                try
                {
                    enemyPreset.SavePreset();
                }
                catch (Exception ex)
                {
                    await Modal(MessageBoxManager.GetMessageBoxStandard(
                        messages.Get(presetFileError),
                        messages.Get(MiscSetup.CreateFileError, ex.Message),
                        ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    return;
                }
            }
            ItemPreset itemPreset = newOpts.ItemPresetValue;
            if (itemPreset != null)
            {
                // Delete existing model first
                Customitem = false;
                try
                {
                    itemPreset.SavePreset();
                }
                catch (Exception ex)
                {
                    await Modal(MessageBoxManager.GetMessageBoxStandard(
                        messages.Get(presetFileError),
                        messages.Get(MiscSetup.CreateFileError, ex.Message),
                        ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                    return;
                }
            }
            if (newOpts.HelperConfig != null)
            {
                try
                {
                    HelperOptions.ImportSettings(HelperConfigPath, newOpts.HelperConfig);
                }
                catch (Exception)
                {
                    // Don't bother for now
                }
            }
            SetOptionsFromString(newOpt.ToString());
        }

        private static string? GetHeuristicExe()
        {
            FileInfo exeFile;
            DirectoryInfo? dir = new(".");
            while (dir != null)
            {
                exeFile = new FileInfo(Path.Combine(dir.FullName, ExeName));
                if (exeFile.Exists)
                {
                    return exeFile.FullName;
                }
                dir = dir.Parent;
            }
            exeFile = new FileInfo(DefaultExePath);
            if (exeFile.Exists)
            {
                return exeFile.FullName;
            }
            return null;
        }
        
        private EnemyAnnotations? enemyAnn;
        private bool LoadEnemyAnnotations([NotNullWhen(false)] out string? error)
        {
            bool loaded = LoadAnnotations("diste/Base/enemy.txt", ref enemyAnn, out error);
            if (enemyAnn != null)
            {
                // Blank this out to improve memory slightly maybe, as this config is read separately within randomizer
                // Well, it's needed
                enemyAnn.Enemies = null;
            }
            return loaded;
        }

        private AnnotationData.Annotations? itemAnn;
        private bool LoadItemAnnotations([NotNullWhen(false)] out string? error)
        {
            return LoadAnnotations("diste/Base/annotations.txt", ref itemAnn, out error);
        }

        private AnnotationData.PresetInfo? presetAnn;
        private bool LoadPresetAnnotations([NotNullWhen(false)] out string? error)
        {
            return LoadAnnotations("diste/Base/iteminfo.txt", ref presetAnn, out error);
        }

        private static bool LoadAnnotations<T>(string configPath, [NotNullWhen(false)] ref T? value, [NotNullWhen(false)] out string? error)
        {
            error = null;
            if (value != null) return true;
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                using (StreamReader reader = File.OpenText(configPath))
                {
                    value = deserializer.Deserialize<T>(reader);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private async void ShowBossMessages(string? initialPath = null)
        {
            EnemyAnnotations? locEnemyAnn = null;
            if (!LoadAnnotations("diste/Base/enemy.txt", ref locEnemyAnn, out string? err))
            {
                await Modal(MessageBoxManager.GetMessageBoxStandard(
                    messages.Get(presetFileError),
                    err,
                    ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
                return;
            }
#if WINFORMS
            using (EnemyMessagesForm form = new EnemyMessagesForm(messages, locEnemyAnn, initialPath))
            {
                ShowWinFormsDialog(form);
            }
#endif
        }

#if WINFORMS
        private const string iconAssetPath = "avares://EldenRingRandomizer.Avalonia/Assets/elden.ico";

        // Some WinForms utilities. Hopefully these won't be needed again.
        private System.Drawing.Icon? thisIcon = null;
        private System.Windows.Forms.DialogResult ShowWinFormsDialog<T>(T form) where T : System.Windows.Forms.Form
        {
            thisIcon ??= GetWinFormsIcon(iconAssetPath);
            IPlatformHandle? platformHandle = MainWindow()?.TryGetPlatformHandle();
            if (platformHandle != null)
            {
                Win32WindowWrapper owner = new(platformHandle.Handle);
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
                form.Icon = thisIcon;
                return form.ShowDialog(owner);
            }
            else
            {
                return form.ShowDialog();
            }
        }

        public class Win32WindowWrapper : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }

            public Win32WindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }
        }

        private static System.Drawing.Icon GetWinFormsIcon(string assetUri)
        {
            Stream assetLoader = AssetLoader.Open(new Uri(assetUri));
            using (MemoryStream memoryStream = new MemoryStream())
            {
                assetLoader.CopyTo(memoryStream);
                memoryStream.Position = 0;
                return new System.Drawing.Icon(memoryStream);
            }
        }
#endif
    }
}
