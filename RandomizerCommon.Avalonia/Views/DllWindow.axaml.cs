using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using System;
using System.Linq;

namespace RandomizerCommon.Views;

public partial class DllWindow : ReactiveWindow<DllViewModel>
{
    public DllWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(_ => Close())));
        Closing += HandleClosing;
    }

    private async void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        // Always cancel, then manually close if event finishes
        e.Cancel = true;

        if (ViewModel!.IsModified())
        {
            // Can't set DialogResult here, so use custom field instead
            ButtonResult result = await MessageBoxManager.GetMessageBoxStandard(
                "Confirm",
                "Save changes before exiting?",
                ButtonEnum.YesNoCancel,
                MsBox.Avalonia.Enums.Icon.Question,
                WindowStartupLocation.CenterOwner)
                .ShowWindowDialogAsync(this);
            if (result == ButtonResult.Yes)
            {
                // This skips the observable stuff
                ViewModel.Ok();
            }
            else if (result == ButtonResult.Cancel)
            {
                return;
            }
        }

        Closing -= HandleClosing;
        Close();
    }
}