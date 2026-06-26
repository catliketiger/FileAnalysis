using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 结构树 ViewModel — 将 StructureNode 树展平为 WPF TreeView 可绑定的层次集合
/// </summary>
public partial class StructureTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private StructureNode? _rootNode;

    [ObservableProperty]
    private StructureNode? _selectedNode;

    public ObservableCollection<TreeItemViewModel> RootItems { get; } = new();

    /// <summary>
    /// 加载新的结构树
    /// </summary>
    public void LoadTree(StructureNode root)
    {
        RootNode = root;
        RootItems.Clear();
        foreach (var child in root.Children)
        {
            RootItems.Add(CreateItem(child));
        }
    }

    /// <summary>
    /// 搜索树节点（按名称匹配，不区分大小写），选中并展开找到的项
    /// </summary>
    public bool SearchTree(string searchText)
    {
        if (RootNode == null || string.IsNullOrWhiteSpace(searchText)) return false;

        // 在 TreeItemViewModel 树中搜索（确保能选中 UI 中的对应项）
        var found = FindTreeItemByName(RootItems, searchText);
        if (found != null)
        {
            ClearAllSelection(RootItems);
            ExpandPathToTreeItem(found);
            found.IsSelected = true;
            SelectedNode = found.Node;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 遍历 TreeItemViewModel 树按名称匹配
    /// </summary>
    private static TreeItemViewModel? FindTreeItemByName(IEnumerable<TreeItemViewModel> items, string searchText)
    {
        foreach (var item in items)
        {
            if (item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return item;

            var found = FindTreeItemByName(item.Children, searchText);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 清除树
    /// </summary>
    public void Clear()
    {
        RootNode = null;
        SelectedNode = null;
        RootItems.Clear();
    }

    /// <summary>
    /// 选中与指定偏移匹配的节点
    /// </summary>
    public StructureNode? SelectNodeByOffset(long offset)
    {
        if (RootNode == null) return null;
        var node = RootNode.FindByOffset(offset);
        if (node != null && node != RootNode)
        {
            ClearAllSelection(RootItems);
            var found = FindTreeItemByNode(RootItems, node);
            if (found != null)
            {
                ExpandPathToTreeItem(found);
                found.IsSelected = true;
            }
            SelectedNode = node;
        }
        return node;
    }

    /// <summary>
    /// 遍历 TreeItemViewModel 树匹配 StructureNode
    /// </summary>
    private static TreeItemViewModel? FindTreeItemByNode(IEnumerable<TreeItemViewModel> items, StructureNode targetNode)
    {
        foreach (var item in items)
        {
            if (item.Node == targetNode)
                return item;
            var found = FindTreeItemByNode(item.Children, targetNode);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 清除所有 TreeItem 的选中状态
    /// </summary>
    public static void ClearAllSelection(IEnumerable<TreeItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.IsSelected = false;
            ClearAllSelection(item.Children);
        }
    }

    /// <summary>
    /// 展开到目标项的路径
    /// </summary>
    private static void ExpandPathToTreeItem(TreeItemViewModel item)
    {
        var current = item.Node;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private static TreeItemViewModel CreateItem(StructureNode node)
    {
        var item = new TreeItemViewModel(node);
        foreach (var child in node.Children)
        {
            item.Children.Add(CreateItem(child));
        }
        return item;
    }
}

/// <summary>
/// WPF TreeView 可绑定的树节点
/// </summary>
public class TreeItemViewModel : ObservableObject
{
    private readonly StructureNode _node;
    private bool _isSelected;

    public TreeItemViewModel(StructureNode node)
    {
        _node = node;
        node.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(StructureNode.IsExpanded))
                OnPropertyChanged(nameof(IsExpanded));
        };
    }

    public string Name => _node.Name;
    public long Offset => _node.Offset;
    public long Length => _node.Length;
    public string DataType => _node.DataType.ToString();
    public double Confidence => _node.Confidence;
    public bool IsConfidenceAvailable => _node.IsConfidenceAvailable;
    public StructureNodeSource Source => _node.Source;
    public bool IsExpanded
    {
        get => _node.IsExpanded;
        set => _node.IsExpanded = value;
    }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public StructureNode Node => _node;

    public ObservableCollection<TreeItemViewModel> Children { get; } = new();
}
