using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon.Views;

public partial class HelperWindow : ReactiveWindow<HelperViewModel>
{
    public HelperWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(_ => Close())));
    }

    // NumericUpDown kind of broken, property binding must be nullable or else ugly error
    public void Option_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown num)
        {
            return;
        }
        if (num.Value is null)
        {
            if (num.DataContext is HelperViewModel.OptionState opt && opt.Info.Default is int def)
            {
                num.Value = def;
            }
            else
            {
                num.Value = num.Minimum;
            }
        }
    }
}
