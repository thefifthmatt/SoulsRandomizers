using DynamicData;
using DynamicData.Binding;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using RandomizerCommon.Localization;
using RandomizerCommon.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using static RandomizerCommon.ItemPreset;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Models.ItemRepository;
using static RandomizerCommon.RandomizerOptions;
using static RandomizerCommon.ViewModels.ItemPanelViewModel;
using static SoulsIds.Universe;

namespace RandomizerCommon.ViewModels
{
    public partial class ItemPresetViewModel : ViewModelBase
    {
        public readonly IOptionsProvider OptionsProvider;
        public readonly ItemRepository Repo;
        
        [Reactive]
        private ObservableCollection<ItemPanelViewModel> _panels = new();
        [Reactive]
        private ItemPanelViewModel? _selectedItem;
        [Reactive]
        private bool _isExpanded = true;
        
        [Reactive]
        private ItemPreset _preset;
        [Reactive]
        private string _presetDesc = "";

        // Misc global stuff
        [Reactive] private LocationData.AreaSiloType _overallSilo;
        [Reactive] private LocationData.AreaSiloType _keyItemSilo;
        [Reactive] private DisplayItemList _norandomItems;
        [Reactive] private DisplayItemList _norandomLocs;
        [Reactive] private DisplayItemList _remove;
        [Reactive] private DisplayItemList _multiKeyItems;
        [Reactive] private ObservableCollection<DisplayItemShard> _itemShards;
        // Bias is synced with main viewmodel
        [Reactive] private int _bias;
        [Reactive] private string _biasLabel;
        [Reactive] private bool _manualBias;
        // The exponent for the weight of the most difficult and late location in the game, for placing most items. The lowest weight is 1 and highest weight is 2^spread, and other weights are gradually spaced in between them.
        // If this is 0, all locations have the same weight. If it's 1, the most desirable location is twice as likely for the most desirable item.
        [Reactive] private double? _itemSpread = 0;
        // The exponent for the weight of the most difficult and late location in the game, for placing important items.
        // This makes major key items more likely to be placed at bosses.
        // It works the same way as item spread, but has a higher max value because major key items are limited to specific areas by logic, and locations within specific areas have closer weights.
        [Reactive] private double? _keyItemSpread = 0;
        // Every time a major key item is placed, the key item selection weight for areas blocked by the item are multiplied by this value, leading to key item chains.
        // The weight for areas is also divided by this amount when an item is placed there, cancelling out the chain multiplier and overall spreading out key items.
        [Reactive] private double? _keyItemChain = 1;
        // Alternative to chain weight which can spread out key items without also chaining them.
        // The weight for areas is divided by this amount when an item is placed there. This stacks multiplicatively with chain weight if both are present.
        [Reactive] private double? _keyItemRegionSpread = 1;
        
        public ItemPresetViewModel(ItemRepository repo, ItemPreset preset, IOptionsProvider optionsProvider)
        {
            Repo = repo;
            OptionsProvider = optionsProvider;
            Preset = preset;
            Messages messages = Messages.GetInstance();
            PresetDesc = Preset.FullDescription(messages);

            RandomizerOptions current = optionsProvider.MakeOptions();
            Bias = current.Difficulty;

            this.WhenAnyValue(x => x.Bias, x => x.ManualBias)
                .Subscribe(_ =>
                {
                    BiasLabel = $"{Bias}%";
                    if (!ManualBias)
                    {
                        BiasWeights weights = BiasWeights.Calculate(Bias, SoulsIds.GameSpec.FromGame.ER);
                        ItemSpread = weights.ItemSpread;
                        KeyItemSpread = weights.KeyItemSpread;
                        KeyItemChain = weights.KeyItemChain;
                        KeyItemRegionSpread = weights.KeyItemRegionSpread;
                    }
                });
            this.WhenAnyValue(x => x.OverallSilo)
                .Subscribe(_ =>
                {
                    if (OverallSilo == LocationData.AreaSiloType.DLC)
                    {
                        KeyItemSilo = LocationData.AreaSiloType.DLC;
                    }
                });

            // Construct trees based on combination of default preset (contains all required groups, even if disabled)
            // and the given preset. This model is the source of truth, rather than the preset.
            InitializePanels();

            Observable.Merge(
                this.WhenAnyValue(x => x.ItemSpread).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.KeyItemSpread).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.KeyItemChain).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.KeyItemRegionSpread).Select(_ => Unit.Default).Skip(1))
                // Guard against NumericUpDown spam
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Subscribe(_ => {
                    if (ManualBias)
                    {
                        UpdateModel();
                    }
                });
            Observable.Merge(
                this.WhenAnyValue(x => x.OverallSilo).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.KeyItemSilo).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.ManualBias).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.NorandomItems).SelectMany(DisplayItemList.WhenModified),
                this.WhenAnyValue(x => x.NorandomLocs).SelectMany(DisplayItemList.WhenModified),
                this.WhenAnyValue(x => x.Remove).SelectMany(DisplayItemList.WhenModified),
                this.WhenAnyValue(x => x.ItemShards).SelectMany(t => t.ToObservableChangeSet().AutoRefresh()).Select(_ => Unit.Default).Skip(1))
                // Note UpdateModel can also get spammed a bit from individual panels
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(_ =>
                {
                    UpdateModel();
                });
            // Does not need to be called unless language changes
            AvaloniaMessages.OnCultureChange().Subscribe(_ =>
            {
                UpdateText();
            });
        }

        public List<T>? FromDisplayList<T>(DisplayItemList? list) where T : SwitchRef
        {
            return list?.Items
                .Where(i => i.Enabled)
                .Select(i => i.Obj)
                .Cast<T>()
                .ToList();
        }

        public DisplayItemList ToDisplayList<T>(ItemListType type, List<T>? inits, List<T>? defs, bool editable = true) where T : SwitchRef
        {
            inits ??= new();
            defs ??= new();
            List<string> ids = (editable ? defs.Concat(inits) : defs).Select(i => i.RefID).Distinct().ToList();
            List<DisplayItem> items = new();
            if (ids.Count > 0)
            {
                // Non-item options do influence editability of some individual options, TODO recalc this as needed
                RandomizerOptions opt = OptionsProvider.MakeOptions();
                foreach (string id in ids)
                {
                    T? init = inits.Find(i => i.RefID == id);
                    T? def = defs.Find(i => i.RefID == id);
                    T? obj = init ?? def;
                    if (obj == null) continue;
                    DisplayItem item = new()
                    {
                        Obj = obj,
                        Def = def,
                        ListEditable = editable,
                        Enabled = init != null,
                        LocalizedText = Repo.ToDisplay(obj),
                    };
                    item.UpdateFromOptions(opt);
                    items.Add(item);
                }
            }
            List<string>? defaultItems = null;
            if (defs != null)
            {
                // Fill in defaultItems based on default options, add local reset button?
            }
            // Source is mainly needed iff editable, but maybe useful for validation and such
            DisplayItemSource source = new DisplayItemSource() { Type = type, Items = Repo.GetAutocompleteItems(type) };
            DisplayItemList list = new DisplayItemList(this, editable, source, items, defaultItems);
            return list;
        }

        // This has some overlap with DisplayItem
        // Item shards could in theory use DisplayItemList with some extra state and completely different render path but then commonality is then "a list"
        public partial class DisplayItemShard : ReactiveObject
        {
            [Reactive] private ItemRef _obj;
            [Reactive] private int? _reqCount;
            [Reactive] private int? _count;

            [Reactive] private string _localizedText;

            public DisplayItemShard(ItemRef obj, string localizedText, int? count = null, int? reqCount = null)
            {
                Obj = obj;
                LocalizedText = localizedText;
                Count = count ?? 1;
                ReqCount = reqCount ?? Count;
                // Try to make this not annoying to do en masse
                this.WhenAnyValue(x => x.Count)
                    .Subscribe(_ =>
                    {
                        if (Count is null)
                        {
                            Count = ReqCount ?? 1;
                        }
                        if (ReqCount != null && ReqCount > Count)
                        {
                            ReqCount = Count;
                        }
                    });
                this.WhenAnyValue(x => x.ReqCount)
                    .Subscribe(_ =>
                    {
                        if (ReqCount is null)
                        {
                            ReqCount = Count ?? 1;
                        }
                        if (Count != null && ReqCount > Count)
                        {
                            Count = ReqCount;
                        }
                    });
            }

            public void UpdateText(ItemRepository repo)
            {
                LocalizedText = repo.ToDisplay(Obj);
            }
        }

        public DisplayItemShard ToDisplayShard(ItemShard shard)
        {
            return new DisplayItemShard(shard.Item, Repo.ToDisplay(shard.Item), shard.Count, shard.ReqCount);
        }

        public ItemShard FromDisplayShard(DisplayItemShard shard)
        {
            int count = shard.Count ?? 1;
            int reqCount = Math.Min(shard.ReqCount ?? count, count);
            return new ItemShard { Item = shard.Obj, Count = count, ReqCount = reqCount };
        }

        public void AddMultiItem(ItemRef item)
        {
            DisplayItemShard? existing = ItemShards.Where(i => item.Equals(i.Obj)).FirstOrDefault();
            if (existing != null)
            {
                return;
            }
            ItemShards.Add(new DisplayItemShard(item, Repo.ToDisplay(item)));
            // Dunno can't get ToObservableChangeSet by itself to make this happen
            UpdateModel();
        }

        [ReactiveCommand]
        public void RemoveMultiItem(DisplayItemShard shard)
        {
            ItemShards.Remove(shard);
        }

        private void UpdateItems(Action<DisplayItem> action)
        {
            foreach (ItemPanelViewModel child in Panels)
            {
                child.UpdateItems(action);
            }
            NorandomItems?.UpdateItems(action);
            NorandomLocs?.UpdateItems(action);
            Remove?.UpdateItems(action);
        }

        public void UpdateFromOptions(RandomizerOptions opt)
        {
            UpdateItems(item => item.UpdateFromOptions(opt));
        }

        private void UpdateText()
        {
            UpdateItems(item => item.UpdateText(Repo));
            PresetDesc = Preset.FullDescription(Messages.GetInstance());
        }

        private bool loading = false;
        public void UpdateModel()
        {
            if (loading) return;

            Preset.OverallSilo = OverallSilo;
            Preset.KeyItemSilo = KeyItemSilo;
            Preset.NorandomItems = FromDisplayList<ItemRef>(NorandomItems);
            Preset.NorandomLocations = FromDisplayList<LocationRef>(NorandomLocs);
            Preset.RemoveItems = FromDisplayList<ItemRef>(Remove);
            Preset.ItemShards = ItemShards.Count == 0 ? null : ItemShards.Select(FromDisplayShard).ToList();
            // If ManualBias and any null, just keep previous weights
            if (ManualBias && (ItemSpread != null && KeyItemSpread != null && KeyItemChain != null && KeyItemRegionSpread != null))
            {
                float truncate(double? d) => (float)(int)((d ?? 0) * 100) / 100;
                BiasWeights weights = new()
                {
                    ItemSpread = truncate(ItemSpread),
                    KeyItemSpread = truncate(KeyItemSpread),
                    KeyItemChain = truncate(KeyItemChain),
                    KeyItemRegionSpread = truncate(KeyItemRegionSpread),
                };
                weights.Clamp();
                Preset.BiasWeights = weights;
            }
            else if (!ManualBias)
            {
                Preset.BiasWeights = null;
            }

            foreach (ItemPanelViewModel panel in Panels)
            {
                if (panel.Type == TabType.Placements)
                {
                    Preset.Placements = panel.Children!
                        .Where(t => t.Type == TabType.Placement)
                        .Select(t => t.GetModelObject<Placement>())
                        .ToList();
                }
                else if (panel.Type == TabType.LocationGroup)
                {
                    Preset.LocationGroups = [panel.GetModelObject<LocationGroup>()];
                }
                else if (panel.Type == TabType.PremiumGroups)
                {
                    Preset.PremiumGroups = panel.Children!
                        .Where(t => t.Type == TabType.PremiumGroup)
                        .Select(t => t.GetModelObject<PremiumGroup>())
                        .ToList();
                }
                else if (panel.Type == TabType.Silos)
                {
                    Preset.CustomSilos = panel.Children!
                        .Where(t => t.Type == TabType.ItemSilo)
                        .Select(t => t.GetModelObject<CustomSilo>())
                        .ToList();
                }
            }
            PresetDesc = Preset.FullDescription(Messages.GetInstance());
#if DEBUG
            // TODO: Make saving indicator in UI? For regular options as well
            Console.WriteLine("Saving item preset");
#endif
            Preset.SavePreset();
        }

        [Localize]
        private static readonly Text resetConfirm = new Text("Reset all custom item placement settings to their original values?\nThis erases all existing configuration. Export an options file first to restore it later.", "ItemPreset_resetConfirm");

        [ReactiveCommand]
        public async void Reset()
        {
            Messages messages = Messages.GetInstance();
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                messages.Get(FormText.ConfirmTitleText),
                messages.Get(resetConfirm),
                ButtonEnum.OkCancel, Icon.Question, Avalonia.Controls.WindowStartupLocation.CenterOwner));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            RandomizerOptions current = OptionsProvider.MakeOptions();
            // Try doing this in-place
            Preset = Repo.DefaultPreset.ProcessBuiltins(current);
            InitializePanels();
            UpdateModel();
        }

        [ReactiveCommand]
        public async void ShowItemList(DisplayItem obj)
        {
            string text;
            if (obj.Obj is ItemRef item)
            {
                
                text = Repo.GetItemGroupDesc(item);
            }
            else if (obj.Obj is LocationRef loc)
            {
                text = Repo.ListLocations(loc);
            }
            else return;
            await Modal(MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = obj.LocalizedText,
                ContentMessage = text,
                ButtonDefinitions = ButtonEnum.Ok,
                EscDefaultButton = ClickEnum.Ok,
                EnterDefaultButton = ClickEnum.Ok,
                MaxHeight = 500,
                MinWidth = 400,
                MaxWidth = 1000,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            }));
        }

        public void AddedPanel(ItemPanelViewModel panel)
        {
            SelectedItem = panel;
            UpdateModel();
        }

        public void RemoveSubpanel(ItemPanelViewModel panel)
        {
            if (!panel.IsCustom || !TabParents.TryGetValue(panel.Type, out TabType parentType))
            {
                return;
            }
            ItemPanelViewModel? treeParent = Panels.ToList().Find(p => p.Type == parentType);
            if (treeParent?.Children is null)
            {
                return;
            }
            int childIndex = treeParent.Children.IndexOf(panel);
            if (childIndex == -1)
            {
                return;
            }
            SelectedItem = null;
            treeParent.Children.RemoveAt(childIndex);
            SelectedItem = treeParent.Children[Math.Min(childIndex, treeParent.Children.Count - 1)] ?? Panels.First();
            UpdateModel();
        }

        // Sets panels based on current Preset
        private void InitializePanels()
        {
            loading = true;

            using (DelayChangeNotifications())
            {
                ItemPreset defPreset = Repo.DefaultPreset;
                OverallSilo = Preset.OverallSilo;
                KeyItemSilo = Preset.KeyItemSilo;
                NorandomItems = ToDisplayList(ItemListType.AllItems, Preset.NorandomItems, defPreset.NorandomItems);
                NorandomLocs = ToDisplayList(ItemListType.NorandomLocations, Preset.NorandomLocations, defPreset.NorandomLocations);
                Remove = ToDisplayList(ItemListType.PlacementItems, Preset.RemoveItems, defPreset.RemoveItems);
                MultiKeyItems = ToDisplayList<ItemRef>(ItemListType.MultiKeyItems, null, null);
                ItemShards = Preset.ItemShards == null ? [] : new(Preset.ItemShards.Select(ToDisplayShard));
                ManualBias = Preset.BiasWeights != null;
                if (Preset.BiasWeights != null)
                {
                    ItemSpread = Preset.BiasWeights.ItemSpread;
                    KeyItemSpread = Preset.BiasWeights.KeyItemSpread;
                    KeyItemChain = Preset.BiasWeights.KeyItemChain;
                    KeyItemRegionSpread = Preset.BiasWeights.KeyItemRegionSpread;
                }
            }

            // Don't check here that Placements etc contain all required types. Assume they are all present based on preprocessing.
            Panels = new()
            {
                new ItemPanelViewModel(this, TabType.Overview),
                new ItemPanelViewModel(this, TabType.Bias),
                new ItemPanelViewModel(this, TabType.SharedOptions),
                new ItemPanelViewModel(this, TabType.ItemShards),
                new ItemPanelViewModel(this, TabType.Placements,
                    children: Preset.Placements.Select(p => new ItemPanelViewModel(this, TabType.Placement, p))),
                new ItemPanelViewModel(this, TabType.LocationGroup, Preset.ImportantGroup),
                new ItemPanelViewModel(this, TabType.PremiumGroups,
                    children: Preset.PremiumGroups.Select(p => new ItemPanelViewModel(this, TabType.PremiumGroup, p))),
                new ItemPanelViewModel(this, TabType.Silos,
                    children: new[] { new ItemPanelViewModel(this, TabType.AreaSilo) }
                        .Concat(Preset.CustomSilos.Select(p => new ItemPanelViewModel(this, TabType.ItemSilo, p))))
            };
            SelectedItem = Panels.First();

            loading = false;
        }
    }
}