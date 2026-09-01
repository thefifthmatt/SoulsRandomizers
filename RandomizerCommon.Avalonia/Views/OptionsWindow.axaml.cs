using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using System;

namespace RandomizerCommon.Views;

public partial class OptionsWindow : ReactiveWindow<OptionsViewModel>
{
    public OptionsWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
    }
}
