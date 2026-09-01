using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using System;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace RandomizerCommon.Views;

public partial class ItemPresetView : ReactiveUserControl<ItemPresetViewModel>
{
    public ItemPresetView()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<object?> SharedOptionsProperty =
        AvaloniaProperty.Register<ItemPresetView, object?>(nameof(SharedOptions));

    public object? SharedOptions
    {
        get => GetValue(SharedOptionsProperty);
        set => SetValue(SharedOptionsProperty, value);
    }

    // NumericUpDown kind of broken, property binding must be nullable or else ugly error
    public void BiasWeight_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown num)
        {
            return;
        }
        if (num.Value is null)
        {
            num.Value = num.Minimum;
        }
    }
}
