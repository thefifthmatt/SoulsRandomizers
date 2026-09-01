using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using RandomizerCommon.ViewModels;
using ReactiveUI;

namespace RandomizerCommon.Views;

public partial class EnemyPresetWindow : ReactiveWindow<EnemyPresetViewModel>
{
    public EnemyPresetWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
        Closing += HandleClosing;
    }

    private async void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        // Always cancel, then manually close if event finishes
        e.Cancel = true;

        if (!await ViewModel!.ContinueAfterSave())
        {
            return;
        }

        Closing -= HandleClosing;
        Close(ViewModel.SavedPreset);
    }
}