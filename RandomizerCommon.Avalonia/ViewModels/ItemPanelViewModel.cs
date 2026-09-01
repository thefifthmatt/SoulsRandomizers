using Avalonia.Collections;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using RandomizerCommon.Models;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static RandomizerCommon.ItemPreset;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Models.ItemRepository;
using static SoulsIds.Universe;

namespace RandomizerCommon.ViewModels
{
    public partial class ItemPanelViewModel : ViewModelBase
    {
        public enum TabType
        {
            Overview,
            // Custom item and treeview explanation. Opaque view is transcluded from mod
            Bias,
            SharedOptions,
            ItemShards,
            // Placement explanation and adding custom
            Placements,
            Placement,
            // Location groups (currently only important locations, so top-level is unused)
            LocationGroups,
            LocationGroup,
            // TODO rename?
            // Restricted locations, limited locations, premium locations
            PremiumGroups,
            PremiumGroup,
            // Top-level silos etc
            Silos,
            AreaSilo,
            ItemSilo,
        }
        public static readonly Dictionary<TabType, TabType> TabParents = new()
        {
            [TabType.Placement] = TabType.Placements,
            [TabType.PremiumGroup] = TabType.PremiumGroups,
            [TabType.ItemSilo] = TabType.Silos,
        };

        // This excludes strings already covered in ItemPreset
        [Localize]
        public static readonly Dictionary<TabType, Text> TabTypeDesc = MapText<TabType>("ItemPanel_TabTypeDesc", new()
        {
            [TabType.Overview] = "Overview",
            [TabType.Bias] = "Bias",
            [TabType.SharedOptions] = "Misc options",
            [TabType.ItemShards] = "Key item shards",
            [TabType.PremiumGroups] = "Premium locations",
            [TabType.Silos] = "Silos",
            [TabType.AreaSilo] = "DLC silos",
        });
        [Localize]
        public static readonly Dictionary<TabType, Text> TabTypeExplain = MapText<TabType>("ItemPanel_TabTypeExplain", new()
        {
            [TabType.Overview] = "Custom item placement is an advanced mode for detailed configuration of items and locations. Settings configured in custom item placement won't apply anymore if it's disabled. Select any category on the left to configure placement rules. In some cases, you can add new custom rules under root categories.",
            [TabType.ItemShards] = "When key items are split into shards, you need to acquire shards to use the key. Randomization may fail if there are too many shards and too few key item locations. If that happens, consider allowing multiple key items per location in the Logic tab. If you set 0 required shards, the item is no longer needed.",
            [TabType.Placements] = "Item placement rules control where individual items can or cannot be placed. If an item is configured in multiple rules, all constraints apply to it at once. It is easily possible to create impossible combinations of rules where there are not enough locations to fit the items, or vice versa, especially if items have separate region constraints. In those cases, the rules may be ignored or cause randomization failures.",
            [TabType.PremiumGroups] = "Premium location rules control which items can be placed in particular locations. Ideally there are many more items than locations per rule. If a location is configured in multiple rules, the only allowed items come from the intersection of those rules. If this leaves no valid items for a location, the rule is ignored and a warning is added to spoiler logs. If there are not enough items for a location, the rules may be ignored. Note that item placement rules take priority over premium locations, so any items which can only appear at specific locations can always be placed there. If an item is already forbidden from appearing in a location, like unmissable items in missable locations or farmable items in guaranteed locations, putting it in a premium rule won't change that.",
            [TabType.Silos] = "Silos change which groups of locations are randomized among themselves. Note some locations are always randomized separately, like item pickups and farmable enemy drops. Within that, locations can be further split up by region with DLC silos. At the same time, locations can be split up based on groups of items. If an item is configured in multiple item silos, it is added to the first applicable silo only.",
            [TabType.AreaSilo] = "DLC silos allow randomizing base game and DLC items separately. Note that selecting \"Randomize base game and DLC separately\" in the DLC tab or disabling the DLC tab altogether takes priority over these settings. You can override the DLC silo for non-key items with item placement rules.",
            [TabType.ItemSilo] = "Within item silos, individual items replace each other. By-type groups add many smaller silos for every weapon type or armor slot in the game. Normally all items are both sources and targets, but randomization will fail if there are more sources than targets in a silo, which can happen with the option to add guaranteed copies of random enemy drops. If there are more targets than sources, junk items may replace some target items. If items or item locations in a silo have placement restrictions, randomization may fail.",
        });
        [Localize]
        private static readonly Text parentPlacementsText = new Text("Item placements", "PresetItemForm_parentPlacements");
        [Localize]
        private static readonly Text listLocations = new Text("View all {0} selected locations", "PresetItemForm_listLocations");
        [Localize]
        private static readonly Text listLocationsRegular = new Text("If there are significantly more items than available locations, some items may be randomly placed elsewhere.", "PresetItemForm_listLocationsRegular");
        [Localize]
        private static readonly Text listLocationsImportant = new Text("If there are significantly more items than available locations, randomization will fail.", "PresetItemForm_listLocationsImportant");
        [Localize]
        private static readonly Text listLocationsPremium = new Text("If there not enough items for the selected locations, other items may be placed there.", "PresetItemForm_listLocationsPremium");

        [Reactive]
        private TabType _type;

        [Reactive]
        private Text? _nameText;
        [Reactive]
        private string? _name;
        [Reactive]
        private string? _editName;
        [Reactive]
        private ObservableCollection<ItemPanelViewModel>? _children;

        [Reactive] private ItemPresetViewModel _preset;

        // Different cases for this ViewModel. The reference is not updated after the constructor
        [Reactive]
        private Placement? _placement;
        [Reactive]
        private LocationGroup? _locationGroup;
        // Note for premiumgroups: items can't be *added* to pool with this. If there are no valid items, it will be ignored.
        [Reactive]
        private PremiumGroup? _premiumGroup;
        [Reactive]
        private CustomSilo? _customSilo;

        // State splatted out from different cases
        [Reactive] private DisplayItemList? _items;
        [Reactive] private DisplayItemList? _locations;
        [Reactive] private DisplayItemList? _excludes;
        [Reactive] private bool _isDisabled;
        // For Placements, can inform which lists are configurable/present
        [Reactive] private PlacementMode _placementMode;
        [Reactive] private LocationData.ItemSiloType _itemSiloType;

        // This is not fun to express in xaml so make it properties
        [Reactive] private bool _showItems;
        [Reactive] private bool _showLocations;
        [Reactive] private bool _showExcludes;
        [Reactive] private LocationList? _locationList;
        [Reactive] private int _locationCount;
        // Immutable based on tab type
        [Reactive] private Text? _locationLabel;
        [Reactive] private Text? _explainLabel;
        [Reactive] private Text? _locationsWarning;
        [Reactive] private bool _isCustom;  // Can remove, can move, can edit name
        [Reactive] private bool _canAddChildren;
        [Reactive] private bool _canShowMode;
        [Reactive] private bool _canShowItemSilo;
        [Reactive] private bool _canBeDisabled;

        public ItemPanelViewModel(ItemPresetViewModel parent, TabType type, object? model = null, IEnumerable<ItemPanelViewModel>? children = null)
        {
            Preset = parent;
            Type = type;
            Children = children is null ? null : new(children);
            CanAddChildren = TabParents.Values.Contains(type);
            if (CanAddChildren && Children is null) throw new ArgumentNullException(nameof(children));
            // This can be done in binding syntax but it inflicts physical pain
            this.WhenAnyValue(x => x.Items, x => x.ItemSiloType)
                .Subscribe(_ => ShowItems = Items != null && (CustomSilo == null || ItemSiloType == LocationData.ItemSiloType.Manual));
            this.WhenAnyValue(x => x.Locations, x => x.PlacementMode)
                .Subscribe(_ => ShowLocations = Locations != null && (Placement == null || PlacementMode == PlacementMode.Custom));
            this.WhenAnyValue(x => x.Excludes, x => x.PlacementMode)
                .Subscribe(_ => ShowExcludes = Excludes != null && (Placement == null || PlacementMode == PlacementMode.Custom || PlacementMode == PlacementMode.Anywhere));
            // TODO: Localize properly
            ItemPreset defPreset = Preset.Repo.DefaultPreset;
            Messages messages = Messages.GetInstance();
            if (Type == TabType.Placements)
            {
                NameText = parentPlacementsText;
            }
            else if (TabTypeDesc.TryGetValue(Type, out Text? nameText))
            {
                NameText = nameText;
            }
            if (TabTypeExplain.TryGetValue(Type, out Text? explainText))
            {
                ExplainLabel = explainText;
            }
            if (Type == TabType.Placement)
            {
                Placement = model as Placement ?? throw new ArgumentNullException();
                Placement? def = defPreset.GetPlacement(Placement.Type);
                PlacementMeta meta = PlacementMetas[Placement.Type];
                PlacementMode = Placement.Mode;
                if (meta.EditLocs)
                {
                    CanShowMode = true;
                }
                CanBeDisabled = meta.Disable;
                IsDisabled = CanBeDisabled && Placement.Disabled;
                Items = Preset.ToDisplayList(ItemListType.PlacementItems, Placement.Items, def?.Items, meta.EditItems);
                if (meta.EditLocs || def?.Requires != null)
                {
                    Locations = Preset.ToDisplayList(ItemListType.RequirableLocations, Placement.Requires, def?.Requires, meta.EditLocs);
                    LocationLabel = PlacementRequiresText;
                }
                if (meta.EditLocs || def?.Excludes != null)
                {
                    ItemListType exType = meta.GlobalLocs ? ItemListType.NonRegionLocations : ItemListType.AllLocations;
                    Excludes = Preset.ToDisplayList(exType, Placement.Excludes, def?.Excludes, meta.EditLocs);
                }
                IsCustom = Placement.Type == PlacementType.Custom;
                if (IsCustom)
                {
                    Name = Placement.Name;
                }
                else
                {
                    NameText = PlacementTypeNames[Placement.Type];
                }
                if (PlacementTypeDesc.TryGetValue(Placement.Type, out Text explain))
                {
                    ExplainLabel = explain;
                }
                LocationsWarning = Placement.Type == PlacementType.KeyItems ? listLocationsImportant : listLocationsRegular;
            }
            else if (Type == TabType.LocationGroup)
            {
                LocationGroup = model as LocationGroup ?? throw new ArgumentNullException();
                // Currently the only type
                LocationGroup def = defPreset.ImportantGroup;
                PlacementMeta meta = ImportantMeta;
                Locations = Preset.ToDisplayList(ItemListType.RequirableLocations, LocationGroup.Requires, def?.Requires, meta.EditLocs);
                LocationLabel = PlacementOverallText;
                Excludes = Preset.ToDisplayList(ItemListType.NonRegionLocations, LocationGroup.Excludes, def?.Excludes, meta.EditLocs);
                NameText = PlacementModeNames[PlacementMode.Important];
                ExplainLabel = ImportantLocationsDesc;
                LocationsWarning = listLocationsImportant;
            }
            else if (Type == TabType.PremiumGroup)
            {
                PremiumGroup = model as PremiumGroup ?? throw new ArgumentNullException();
                PremiumGroup? def = defPreset.GetPremiumGroup(PremiumGroup.Type);
                CanBeDisabled = true;
                IsDisabled = PremiumGroup.Disabled;
                Items = Preset.ToDisplayList(ItemListType.PlacementItems, PremiumGroup.Items, def?.Items, true);
                Locations = Preset.ToDisplayList(ItemListType.RequirableLocations, PremiumGroup.Locations, def?.Locations, PremiumGroup.Type == PremiumGroupType.Custom);
                LocationLabel = PlacementOverallText;
                IsCustom = PremiumGroup.Type == PremiumGroupType.Custom;
                if (IsCustom)
                {
                    Name = PremiumGroup.Name;
                }
                else
                {
                    NameText = PremiumGroupTypeNames[PremiumGroup.Type];
                }
                LocationsWarning = listLocationsPremium;
            }
            else if (Type == TabType.ItemSilo)
            {
                // TODO: Initialize items or not?
                CustomSilo = model as CustomSilo ?? throw new ArgumentNullException();
                CanBeDisabled = true;
                IsDisabled = CustomSilo.Disabled;
                ItemSiloType = CustomSilo.Type;
                Items = Preset.ToDisplayList(ItemListType.PlacementItems, CustomSilo.Items, null, true);
                IsCustom = true;
                CanShowItemSilo = true;
                Name = CustomSilo.Name;
            }
            else if (model is not null)
            {
                throw new ArgumentException("Unexpected model");
            }
            EditName = Name;

            Observable.Merge(
                this.WhenAnyValue(x => x.IsDisabled).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.PlacementMode).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.ItemSiloType).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.Name).Select(_ => Unit.Default).Skip(1),
                DisplayItemList.WhenModified(Items),
                DisplayItemList.WhenModified(Locations),
                DisplayItemList.WhenModified(Excludes))
                .Subscribe(_ =>
                {
                    UpdateLocationList();
                    Preset.UpdateModel();
                });
            UpdateLocationList();
        }

        private void UpdateLocationList()
        {
            // Currently, Locations means only requirable locations
            if (Locations != null && (Placement == null || PlacementMode == PlacementMode.Custom))
            {
                static IEnumerable<LocationRef> loclist(DisplayItemList list) => list.Items.Where(i => i.Enabled).Select(i => i.Obj).Cast<LocationRef>();
                LocationList = Preset.Repo.CalculateLocations(loclist(Locations), Excludes == null ? [] : loclist(Excludes));
            }
            else
            {
                LocationList = null;
            }
            if (LocationList != null)
            {
                LocationCount = LocationList.Count;
            }
        }

        private string GetCurrentName() => NameText == null ? Name! : Messages.GetInstance().Get(NameText);

        public T GetModelObject<T>() where T : class
        {
            object? model = null;
            if (Placement != null)
            {
                Placement.Mode = PlacementMode;
                Placement.Items = Preset.FromDisplayList<ItemRef>(Items);
                // Save location lists if not visible; just don't populate if absent in default, to avoid unnecessary diffs
                List<LocationRef>? requires = Preset.FromDisplayList<LocationRef>(Locations);
                Placement.Requires = Placement.Requires != null || (requires != null && requires.Count > 0) ? requires : null;
                Placement.Excludes = Preset.FromDisplayList<LocationRef>(Excludes);
                Placement.Disabled = IsDisabled;
                if (IsCustom && !string.IsNullOrWhiteSpace(Name))
                {
                    Placement.Name = Name;
                }
                model = Placement;
            }
            else if (LocationGroup != null)
            {
                LocationGroup.Requires = Preset.FromDisplayList<LocationRef>(Locations);
                LocationGroup.Excludes = Preset.FromDisplayList<LocationRef>(Excludes);
                model = LocationGroup;
            }
            else if (PremiumGroup != null)
            {
                PremiumGroup.Items = Preset.FromDisplayList<ItemRef>(Items);
                PremiumGroup.Locations = Preset.FromDisplayList<LocationRef>(Locations);
                PremiumGroup.Disabled = IsDisabled;
                if (IsCustom && !string.IsNullOrWhiteSpace(Name))
                {
                    PremiumGroup.Name = Name;
                }
                model = PremiumGroup;
            }
            else if (CustomSilo != null)
            {
                CustomSilo.Type = ItemSiloType;
                CustomSilo.Items = ShowItems ? Preset.FromDisplayList<ItemRef>(Items) : null;
                CustomSilo.Disabled = IsDisabled;
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    CustomSilo.Name = Name;
                }
                model = CustomSilo;
            }
            return model as T ?? throw new Exception($"Internal error: item preset tab {Type} {Name} missing state, cannot be saved with type {typeof(T)}");
        }

        public void UpdateItems(Action<DisplayItem> action)
        {
            if (Children != null)
            {
                foreach (ItemPanelViewModel child in Children)
                {
                    child.UpdateItems(action);
                }
            }
            Items?.UpdateItems(action);
            Locations?.UpdateItems(action);
            Excludes?.UpdateItems(action);
        }

        [ReactiveCommand]
        public void SaveName()
        {
            if (!string.IsNullOrWhiteSpace(EditName))
            {
                Name = EditName;
            }
        }

        [Localize]
        private static readonly Text menuRemoveConfirm = new Text("Remove \"{0}\"?", "ItemPreset_menuRemoveConfirm");

        [ReactiveCommand]
        public async void RemoveChild()
        {
            // Currently, only custom items can be removed and have Name
            Messages messages = Messages.GetInstance();
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                messages.Get(FormText.ConfirmTitleText),
                messages.Get(menuRemoveConfirm, GetCurrentName()),
                ButtonEnum.OkCancel, Icon.Question, Avalonia.Controls.WindowStartupLocation.CenterOwner));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            Preset.RemoveSubpanel(this);
        }

        [ReactiveCommand]
        public void AddChild()
        {
            ItemPanelViewModel child;
            Messages messages = Messages.GetInstance();
            if (Type == TabType.Placements)
            {
                child = new(Preset, TabType.Placement, Placement.MakeCustom(messages));
            }
            else if (Type == TabType.PremiumGroups)
            {
                child = new(Preset, TabType.PremiumGroup, PremiumGroup.MakeCustom(messages));
            }
            else if (Type == TabType.Silos)
            {
                child = new(Preset, TabType.ItemSilo, CustomSilo.MakeCustom(messages));
            }
            else return;
            Children ??= new();
            Children.Add(child);
            Preset.AddedPanel(child);
        }

        [ReactiveCommand]
        public async void ShowLocationList()
        {
            if (LocationList == null)
            {
                return;
            }
            string title = GetCurrentName();
            string text = Preset.Repo.ListLocations(LocationList);
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
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            }));
        }

        public partial class DisplayItemList : ReactiveObject
        {
            public readonly ItemPresetViewModel Preset;
            public bool IsEditable { get; init; }
            public IReadOnlyList<string>? DefaultList { get; init; }
            [Reactive] private DisplayItemSource _source;
            [Reactive] private ObservableCollection<DisplayItem> _items;

            // Properties have been unreliable for this type of notification so use explicit observable
            private Subject<Unit> _modified = new();
            public static IObservable<Unit> WhenModified(DisplayItemList? list) => list is null ? Observable.Empty<Unit>() : list._modified;

            public DisplayItemList(ItemPresetViewModel preset, bool isEditable, DisplayItemSource source, List<DisplayItem> items, IReadOnlyList<string>? defaultList)
            {
                Preset = preset;
                IsEditable = isEditable;
                Source = source;
                items.ForEach(item => item.List = this);
                Items = new(items);
                DefaultList = defaultList;

                bool loading = true;
                var itemChange = Items.ToObservableChangeSet();
                itemChange.Subscribe(_ =>
                {
                    if (!loading)
                    {
                        SaveChanges();
                    }
                });
                itemChange.AutoRefresh(x => x.Obj).Where(s => s.Refreshes > 0).Subscribe(_ => SaveChanges());
                itemChange.AutoRefresh(x => x.Enabled).Where(s => s.Refreshes > 0).Subscribe(_ => SaveChanges());
                loading = false;
            }

            public void UpdateItems(Action<DisplayItem> action)
            {
                foreach (DisplayItem item in Items)
                {
                    action(item);
                }
            }

            public void AddItem(AutocompleteItem item)
            {
                // Escape hatch for special item shard input
                if (Source.Type == ItemListType.MultiKeyItems)
                {
                    Preset.AddMultiItem((ItemRef)item.Obj);
                    return;
                }
                // Otherwise, add/enable
                string id = item.Obj.RefID;
                DisplayItem? existing = Items.Where(i => i.Obj.RefID == id).FirstOrDefault();
                if (existing != null)
                {
                    if (existing.CanChangeEnabled)
                    {
                        existing.Enabled = true;
                    }
                }
                else
                {
                    Items.Add(new DisplayItem() { Obj = item.Obj, LocalizedText = item.Name, Enabled = true, List = this });
                }
            }

            public void RemoveItem(DisplayItem item)
            {
                if (!item.CanChangeEnabled)
                {
                    return;
                }
                if (item.Def == null)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.Enabled = false;
                }
            }

            // Mutates item in-place if that's possible, or adds/removes other items
            public void SwapItem(DisplayItem item, ItemRef newItem)
            {
                DisplayItem? existing = Items.Where(i => newItem.Equals(i.Obj)).FirstOrDefault();
                if (existing != null)
                {
                    // If target exists, enable it and remove/disable self
                    if (existing.CanChangeEnabled)
                    {
                        existing.Enabled = true;
                        RemoveItem(item);
                    }
                }
                else if (item.Def == null)
                {
                    item.SetItemObj(newItem);
                }
                else
                {
                    // For items with defaults, add target and remove self if possible
                    Items.Add(new DisplayItem() { Obj = newItem, LocalizedText = Preset.Repo.ToDisplayItem(newItem), Enabled = true, List = this });
                    RemoveItem(item);
                }
            }

            private void SaveChanges()
            {
                _modified.OnNext(Unit.Default);
            }
        }

        public partial class DisplayItemSource : ReactiveObject
        {
            public ItemListType Type { get; init; }
            [Reactive] private IEnumerable<AutocompleteItem> _items;

            // This has to match the type of AutoCompleteBox ItemFilter exactly.
            // This is normally done in codebehind but putting it in a WrapPanel forces it to use a DataTemplate
            // which prevents referencing it by name.
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
                return item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            };
        }
        
        // These are used for displayed state
        // The only 'model' fields are Obj and Enabled
        public partial class DisplayItem : ReactiveObject
        {
            // Can be ItemRef or LocationRef, obviously should be consistent within a given list
            [Reactive] public SwitchRef _obj;
            [Reactive] private bool _enabled;
            // When present, UI item is not removable or editable
            public SwitchRef? Def { get; init; }

            // Needed for add/remove operations, and access to overall preset state
            // DisplayItem can sorta function without it, as initial setup is recursive
            public DisplayItemList? List { get; set; }

            // This is mass-updated on language change from UpdateText
            [Reactive] private string _localizedText;
            // Can change based on options forcing defaults
            [Reactive] private bool _defaultEditable = true;
            // Set on init, should match List.IsEditable
            [Reactive] private bool _listEditable = true;
            // Combination of above two
            [Reactive] private bool _canChangeEnabled;

            // Based on Obj, should not change for a given instance. This means the context menu can be shown
            [Reactive] private bool _isGroup;
            // Populated on opening context menu
            [Reactive] private ImmutableList<string>? _filters;
            [Reactive] private bool _showFilters;
            [Reactive] private bool _canClearFilters;

            public DisplayItem()
            {
                // Mainly for accessing ItemRepository and modal dialogs
                this.WhenAnyValue(x => x.Filters, x => x.CanClearFilters, x => x.ListEditable)
                    .Subscribe(_ => ShowFilters = ListEditable && (CanClearFilters || (Filters != null && Filters.Count > 0)));
                this.WhenAnyValue(x => x.DefaultEditable, x => x.ListEditable)
                    .Subscribe(_ => CanChangeEnabled = DefaultEditable && ListEditable);
                this.WhenAnyValue(x => x.Obj)
                    .Subscribe(_ =>
                    {
                        if (Obj is ItemRef item)
                        {
                            IsGroup = item.Group != null;
                            CanClearFilters = item.Content != null || item.Rarity != null;
                            Filters = null;
                        }
                        else if (Obj is LocationRef loc)
                        {
                            IsGroup = RequirableTags.Contains(loc.Tag);
                        }
                    });
            }

            [ReactiveCommand]
            public void Remove()
            {
                // May disable instead of remove
                List?.RemoveItem(this);
            }

            [ReactiveCommand]
            public void Enable()
            {
                // This doesn't account for DisplayList editability
                if (CanChangeEnabled)
                {
                    Enabled = true;
                }
            }

            [ReactiveCommand]
            public void PopulateFilters()
            {
                if (Obj is not ItemRef item || Filters != null || List == null || !ListEditable)
                {
                    return;
                }
                // Law of Demeter who?
                Filters = ImmutableList.ToImmutableList(List.Preset.Repo.GetItemGroupFilters(item));
            }

            [ReactiveCommand]
            public void AddFilter(string filter)
            {
                if (Obj is not ItemRef item)
                {
                    return;
                }
                List?.SwapItem(this, AddItemGroupFilter(item, filter));
            }

            [ReactiveCommand]
            public void ClearFilter()
            {
                if (Obj is not ItemRef item)
                {
                    return;
                }
                List?.SwapItem(this, item.NoFilter());
            }

            public void SetItemObj(ItemRef item)
            {
                Obj = item;
                // This could also go through repo
                LocalizedText = GetItemGroupName(Messages.GetInstance(), item);
            }

            public void UpdateText(ItemRepository repo)
            {
                LocalizedText = repo.ToDisplay(Obj);
            }

            public void UpdateFromOptions(RandomizerOptions opt)
            {
                if (Def != null)
                {
                    if (Def.IsRequired(opt))
                    {
                        Enabled = true;
                        DefaultEditable = false;
                    }
                    else if (Def.IsForbidden(opt))
                    {
                        Enabled = false;
                        DefaultEditable = false;
                    }
                    else
                    {
                        // If it's becoming optional because SwitchOn is turned off, make it turn off here. This is basically only applicable for the dlcstart case.
                        if (!DefaultEditable && Def.ManualSwitchOff != null && !opt[Def.ManualSwitchOff])
                        {
                            Enabled = false;
                        }
                        DefaultEditable = true;
                    }
                }
            }

            public override string ToString() => $"[{LocalizedText}]";
        }

        public record OptionsUpdate(RandomizerOptions Options, string ClickOption);
    }
}