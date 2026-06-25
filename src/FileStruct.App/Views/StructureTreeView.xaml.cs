using System.Windows.Controls;
using FileStruct.App.ViewModels;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class StructureTreeView : UserControl
{
    public StructureTreeView()
    {
        InitializeComponent();
    }

    public TreeViewItem? FindItemByNode(StructureNode node)
    {
        return FindItemInContainer(StructTree.Items, node);
    }

    private TreeViewItem? FindItemInContainer(System.Collections.IEnumerable items, StructureNode node)
    {
        foreach (var item in items)
        {
            if (item is TreeItemViewModel vm && vm.Node == node)
            {
                return (TreeViewItem?)StructTree.ItemContainerGenerator.ContainerFromItem(item);
            }
            if (item is TreeViewItem tvi && tvi.HasItems)
            {
                var found = FindItemInContainer(tvi.Items, node);
                if (found != null) return found;
            }
        }
        return null;
    }
}
