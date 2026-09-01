using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.ReactiveUI;
using RandomizerCommon.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using static RandomizerCommon.Models.ItemRepository;
using static RandomizerCommon.ViewModels.ItemPanelViewModel;

namespace RandomizerCommon.Views;

// TODO: Should probably just set DataContext to DisplayList, splitting up per-property is annoying
public partial class ItemList : ReactiveUserControl<DisplayItemList>
{
    public static readonly StyledProperty<ObservableCollection<DisplayItem>?> ItemsProperty = AvaloniaProperty.Register<ItemList, ObservableCollection<DisplayItem>?>(nameof(Items));

    public ObservableCollection<DisplayItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<DisplayItemSource?> SourceProperty = AvaloniaProperty.Register<ItemList, DisplayItemSource?>(nameof(Source));

    public DisplayItemSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly StyledProperty<bool?> IsEditableProperty = AvaloniaProperty.Register<ItemList, bool?>(nameof(IsEditable));

    public bool? IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public static readonly StyledProperty<object?> LabelProperty = AvaloniaProperty.Register<ItemList, object?>(nameof(Label));

    public object? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // Meant for template-internal usage
    internal static readonly StyledProperty<AvaloniaList<object>> ChildrenProperty = AvaloniaProperty.Register<ItemList, AvaloniaList<object>>(nameof(Children));

    internal AvaloniaList<object> Children
    {
        get => GetValue(ChildrenProperty);
        set => SetValue(ChildrenProperty, value);
    }

    public ItemList()
    {
        InitializeComponent();
        Children = new();
        Items = new();
    }

    private DisplayItemList GetDisplayList()
    {
        return DataContext as DisplayItemList ?? throw new Exception("No list view found");
    }

    private ItemPresetViewModel GetPresetViewModel()
    {
        ItemPresetView? root = this.FindLogicalAncestorOfType<ItemPresetView>();
        if (root != null && root.DataContext is ItemPresetViewModel vm)
        {
            return vm;
        }
        throw new Exception("No outer view found");
    }

    // This makes more sense to do as control, not viewmodel
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Console.WriteLine($"{change.Property}: {change.NewValue}");
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldList)
            {
                oldList.CollectionChanged -= OnCollectionChanged;
            }
            if (change.NewValue is INotifyCollectionChanged newList)
            {
                newList.CollectionChanged += OnCollectionChanged;
            }
            RecalculateChildren();
        }
        else if (change.Property == SourceProperty || change.Property == IsEditableProperty)
        {
            RecalculateChildren();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Ideally DynamicData should be used for this
        RecalculateChildren();
    }

    private void RecalculateChildren()
    {
        try
        {
            // This needs to keep the fixed controls in-place or else their interactivity will break.
            // As a result require everything to be non-null, even if it's not visible.
            if (Children == null || Source == null || Items == null) return;
            int fixedCount = 1;
            if (Children.Count < fixedCount)
            {
                Children.Add(Source);
            }
            else
            {
                Children[0] = Source;
                Children.RemoveRange(fixedCount, Children.Count - fixedCount);
            }
            Children.AddRange(Items);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void Box_ChooseItem(object? sender, PopulateListBox.ChooseItemEventArgs e)
    {
        if (e.Item is AutocompleteItem item)
        {
            GetDisplayList().AddItem(item);
        }
    }

    private void ItemMenu_Opening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || flyout.Target is not Control control)
        {
            return;
        }
        if (control.DataContext is not DisplayItem item)
        {
            return;
        }
        item.PopulateFilters();
    }
}
