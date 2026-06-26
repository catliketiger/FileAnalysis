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

    // 搜索状态：上次搜索文本和当前匹配索引
    private string _lastSearchText = "";
    private int _lastMatchIndex = -1;
    private List<TreeItemViewModel> _lastMatches = new();

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
        ResetSearchState();
    }

    /// <summary>
    /// 搜索树节点（按名称匹配，不区分大小写），支持回车循环到下一个匹配
    /// </summary>
    public bool SearchTree(string searchText)
    {
        if (RootNode == null || string.IsNullOrWhiteSpace(searchText)) return false;

        // 如果搜索词变了，重新收集所有匹配
        if (!string.Equals(_lastSearchText, searchText, StringComparison.Ordinal))
        {
            _lastSearchText = searchText;
            _lastMatches = CollectAllMatches(RootItems, searchText);
            _lastMatchIndex = -1;
        }

        if (_lastMatches.Count == 0) return false;

        // 移动到下一个匹配
        _lastMatchIndex = (_lastMatchIndex + 1) % _lastMatches.Count;
        var found = _lastMatches[_lastMatchIndex];

        ClearAllSelection(RootItems);
        ExpandPathToTreeItem(found);
        found.IsSelected = true;
        SelectedNode = found.Node;
        return true;
    }

    /// <summary>
    /// 收集所有名称匹配的项
    /// </summary>
    private static List<TreeItemViewModel> CollectAllMatches(IEnumerable<TreeItemViewModel> items, string searchText)
    {
        var results = new List<TreeItemViewModel>();
        CollectAllMatchesRecursive(items, searchText, results);
        return results;
    }

    private static void CollectAllMatchesRecursive(IEnumerable<TreeItemViewModel> items, string searchText, List<TreeItemViewModel> results)
    {
        foreach (var item in items)
        {
            if (item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                results.Add(item);
            CollectAllMatchesRecursive(item.Children, searchText, results);
        }
    }

    /// <summary>
    /// 重置搜索状态（文件切换或重新识别时调用）
    /// </summary>
    public void ResetSearchState()
    {
        _lastSearchText = "";
        _lastMatchIndex = -1;
        _lastMatches.Clear();
    }

    /// <summary>当前匹配序号（从0开始）</summary>
    public int GetLastMatchIndex() => _lastMatchIndex;

    /// <summary>总匹配数</summary>
    public int GetTotalMatches() => _lastMatches.Count;

    /// <summary>
    /// 清除树
    /// </summary>
    public void Clear()
    {
        RootNode = null;
        SelectedNode = null;
        RootItems.Clear();
        ResetSearchState();
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
