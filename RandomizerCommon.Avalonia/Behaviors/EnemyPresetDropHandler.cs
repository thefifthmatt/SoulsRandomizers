using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;

namespace RandomizerCommon.Behaviors;

public class EnemyPresetDropHandler : DropHandlerBase
{
    private bool Validate(TreeView treeView, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute)
    {
        // If drag and drop is needed in the future
        return false;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is TreeView treeView)
        {
            return Validate(treeView, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is TreeView treeView)
        {
            return Validate(treeView, e, sourceContext, targetContext, true);
        }
        return false;
    }
}