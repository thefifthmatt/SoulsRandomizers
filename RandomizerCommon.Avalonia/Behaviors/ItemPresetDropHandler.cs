using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using System.Collections.Generic;
using System.Linq;
using RandomizerCommon.ViewModels;

namespace RandomizerCommon.Behaviors;

public class ItemPresetDropHandler : DropHandlerBase
{
    private bool Validate(TreeView treeView, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute)
    {
        if (sourceContext is not ItemPanelViewModel sourceNode
            || targetContext is not ItemPresetViewModel vm
            || treeView.GetVisualAt(e.GetPosition(treeView)) is not Control targetControl
            || targetControl.DataContext is not ItemPanelViewModel targetNode)
        {
            return false;
        }
        if (sourceNode.Type != targetNode.Type || !sourceNode.IsCustom || !targetNode.IsCustom)
        {
            return false;
        }
        if (!ItemPanelViewModel.TabParents.TryGetValue(sourceNode.Type, out var parentType))
        {
            return false;
        }
        ItemPanelViewModel? parent = vm.Panels.Where(p => p.Type == parentType).FirstOrDefault();
        if (parent?.Children == null)
        {
            return false;
        }
        var sourceIndex = parent.Children.IndexOf(sourceNode);
        var targetIndex = parent.Children.IndexOf(targetNode);
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
                        MoveItem(parent.Children, sourceIndex, targetIndex);
                        vm.SelectedItem = sourceNode;
                    }
                    return true;
                }
            default:
                return false;
        }
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