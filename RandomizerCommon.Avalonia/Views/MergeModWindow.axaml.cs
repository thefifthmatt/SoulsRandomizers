using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Markup.Xaml;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using System;

namespace RandomizerCommon.Views;

public partial class MergeModWindow : ReactiveWindow<MergeModViewModel>
{
    public MergeModWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        this.WhenActivated(action => action(ViewModel!.DirectoryCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.RegulationCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.TomlCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.ClearCommand.Subscribe(Close)));
    }
}