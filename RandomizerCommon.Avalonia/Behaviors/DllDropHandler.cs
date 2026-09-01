using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using System.Collections.Generic;
using RandomizerCommon.ViewModels;

namespace RandomizerCommon.Behaviors;

public class DllDropHandler : DropHandlerBase
{
    // Pared down from https://github.com/AvaloniaUI/Avalonia.Xaml.Behaviors/blob/master/samples/DragAndDropSample/Behaviors/ItemsListBoxDropHandler.cs
    private bool Validate<T>(ListBox listBox, DragEventArgs e, object? sourceContext, IList<T> items, bool bExecute)
    {
        if (sourceContext is not T sourceItem
            || listBox.GetVisualAt(e.GetPosition(listBox)) is not Control targetControl
            || targetControl.DataContext is not T targetItem)
        {
            return false;
        }

        var sourceIndex = items.IndexOf(sourceItem);
        var targetIndex = items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        switch (e.DragEffects)
        {
            case DragDropEffects.Move:
                {
                    if (bExecute)
                    {
                        MoveItem(items, sourceIndex, targetIndex);
                    }
                    return true;
                }
            default:
                return false;
        }
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ListBox listBox && targetContext is DllViewModel vm)
        {
            return Validate(listBox, e, sourceContext, vm.Dlls, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ListBox listBox && targetContext is DllViewModel vm)
        {
            return Validate(listBox, e, sourceContext, vm.Dlls, true);
        }
        return false;
    }
}