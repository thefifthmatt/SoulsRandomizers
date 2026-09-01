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
using static RandomizerCommon.Models.EnemyRepository;
using static RandomizerCommon.ViewModels.EnemyPanelViewModel;

namespace RandomizerCommon.Views;

// The code-behind has a lot of overlap with ItemList, but the template and ViewModel usage is fairly different
public partial class EnemyList : ReactiveUserControl<DisplayEnemyList>
{
    public static readonly StyledProperty<ObservableCollection<DisplayEnemy>?> ItemsProperty = AvaloniaProperty.Register<ItemList, ObservableCollection<DisplayEnemy>?>(nameof(Items));

    public ObservableCollection<DisplayEnemy>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<DisplayEnemySource?> SourceProperty = AvaloniaProperty.Register<ItemList, DisplayEnemySource?>(nameof(Source));

    public DisplayEnemySource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly StyledProperty<Messages.Text?> LabelProperty = AvaloniaProperty.Register<ItemList, Messages.Text?>(nameof(Label));

    public Messages.Text? Label
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

    public EnemyList()
    {
        InitializeComponent();
        Children = new();
        Items = new();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
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
        else if (change.Property == SourceProperty)
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

    // Special handler for entity id case, not sure how to make that scale (entity picker flyout?)
    private void Box_KeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is not PopulateListBox box)
        {
            return;
        }
        if (args.Key == Key.Return && box.Text != null && EnemyPresetViewModel.IsEntityId(box.Text, out uint entityId))
        {
            box.Text = "";
            string id = entityId.ToString();
            AutocompleteItem item = new AutocompleteItem(id, id, id);
            ViewModel!.AddItem(item);
        }
    }

    private void Box_ChooseItem(object? sender, PopulateListBox.ChooseItemEventArgs e)
    {
        if (e.Item is AutocompleteItem item)
        {
            ViewModel!.AddItem(item);
        }
    }

    public void Weight_GotFocus(object? sender, GotFocusEventArgs e)
    {
        // Console.WriteLine($"Focus {sender}");
        ViewModel!.WeightFocused = true;
    }

    // NumericUpDown kind of broken, property binding must be nullable or else ugly error
    public void Weight_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Console.WriteLine($"Blur {sender}");
        if (sender is not NumericUpDown num)
        {
            return;
        }
        if (num.Value is null)
        {
            num.Value = num.Minimum;
        }
        ViewModel!.WeightFocused = false;
    }
}
