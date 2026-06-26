using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    // 内部导航标识：SearchTree 设置，阻止 SelectNodeByOffset 覆盖搜索结果
    private bool _isInternalNavigation;

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

        _isInternalNavigation = true;
        try
        {
            var found = _lastMatches[_lastMatchIndex];
            ClearAllSelection(RootItems);
            ExpandPathToTreeItem(found);
            found.IsSelected = true;
            SelectedNode = found.Node;
            return true;
        }
        finally
        {
            _isInternalNavigation = false;
        }
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
    /// 设置内部导航状态（SearchTree/OnSelectedItemChanged 使用时阻止 HexView 回馈覆盖）
    /// </summary>
    public IDisposable BeginInternalNavigation()
    {
        _isInternalNavigation = true;
        return new InternalNavigationGuard(this);
    }

    private readonly struct InternalNavigationGuard : IDisposable
    {
        private readonly StructureTreeViewModel _vm;
        public InternalNavigationGuard(StructureTreeViewModel vm) => _vm = vm;
        public void Dispose() => _vm._isInternalNavigation = false;
    }

    /// <summary>
    /// 添加子节点（增量更新，不重置搜索状态）
    /// </summary>
    public void AddChildNode(StructureNode parent, StructureNode child)
    {
        parent.AddChild(child);
        var childEnd = child.Offset + child.Length;
        if (parent.Length < childEnd)
            parent.Length = childEnd;

        // 增量更新 TreeItemViewModel 树（避免 RefreshTree 全量重建）
        var parentItem = FindTreeItemByNode(RootItems, parent);
        if (parentItem != null)
        {
            parentItem.Children.Add(CreateItem(child));
        }
    }

    /// <summary>
    /// 删除子节点（增量更新，不重置搜索状态）
    /// </summary>
    public void DeleteNode(StructureNode node)
    {
        var parent = node.Parent;
        parent?.RemoveChild(node);

        // 增量更新 TreeItemViewModel 树
        if (parent != null)
        {
            var parentItem = FindTreeItemByNode(RootItems, parent);
            if (parentItem != null)
            {
                var childItem = parentItem.Children.FirstOrDefault(c => c.Node == node);
                if (childItem != null)
                    parentItem.Children.Remove(childItem);
            }
        }
    }

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
    /// 选中与指定偏移匹配的节点（内部导航时跳过，防止覆盖搜索/树节点选中）
    /// </summary>
    public StructureNode? SelectNodeByOffset(long offset)
    {
        // 内部导航（搜索/树选中）正在进行时，阻止 HexView 回馈覆盖树选中
        if (_isInternalNavigation) return null;

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

    /// <summary>从当前 RootNode 刷新 TreeItemViewModel 树</summary>
    public void RefreshTree()
    {
        if (RootNode == null) return;
        var selectedName = SelectedNode?.Name;
        RootItems.Clear();
        foreach (var child in RootNode.Children)
        {
            RootItems.Add(CreateItem(child));
        }
        ResetSearchState();
    }

    /// <summary>
    /// 将 StructureNode 子树导出为 FormatRule JSON
    /// </summary>
    public static string ExportAsJson(StructureNode root, string formatName = "自定义结构",
        byte[]? magicBytes = null, int magicOffset = 0)
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = formatName,
            Description = root.Description ?? "用户自定义文件结构",
            SourcePath = "user",
        };

        if (magicBytes != null && magicBytes.Length > 0)
        {
            rule.Signatures.Add(new FormatSignature
            {
                Name = $"{formatName} Magic",
                Pattern = magicBytes,
                Offset = magicOffset,
            });
        }

        // 将子节点转为 FormatStructure
        foreach (var child in root.Children)
        {
            var structDef = new FormatStructure
            {
                Name = child.Name,
                Type = "struct",
                Fields = ConvertNodeToFields(child).ToList(),
            };
            rule.Structures.Add(structDef);
        }

        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return System.Text.Json.JsonSerializer.Serialize(rule, options);
    }

    private static IEnumerable<FormatField> ConvertNodeToFields(StructureNode node)
    {
        yield return new FormatField
        {
            Name = node.Name,
            Type = FieldDataTypeToRuleType(node.DataType),
            Offset = checked((int)node.Offset),
            Length = checked((int)Math.Min(node.Length, int.MaxValue)),
            Endianness = node.Endianness == FieldEndianness.BigEndian ? "BigEndian" : null,
        };
        foreach (var child in node.Children)
        {
            foreach (var field in ConvertNodeToFields(child))
                yield return field;
        }
    }

    private static string FieldDataTypeToRuleType(FieldDataType dt) => dt switch
    {
        FieldDataType.UInt8 => "uint8",
        FieldDataType.Int8 => "int8",
        FieldDataType.UInt16LE or FieldDataType.UInt16BE => "uint16",
        FieldDataType.Int16LE or FieldDataType.Int16BE => "int16",
        FieldDataType.UInt32LE or FieldDataType.UInt32BE => "uint32",
        FieldDataType.Int32LE or FieldDataType.Int32BE => "int32",
        FieldDataType.UInt64LE or FieldDataType.UInt64BE => "uint64",
        FieldDataType.Int64LE or FieldDataType.Int64BE => "int64",
        FieldDataType.FloatLE or FieldDataType.FloatBE => "float",
        FieldDataType.DoubleLE or FieldDataType.DoubleBE => "double",
        FieldDataType.ASCII => "ascii",
        FieldDataType.UTF8 => "utf8",
        FieldDataType.Bytes => "bytes",
        FieldDataType.Struct => "struct",
        _ => "bytes",
    };

    /// <summary>
    /// 从 FormatRule JSON 导入并构建 StructureNode 树
    /// </summary>
    public static StructureNode ImportFromJson(string json)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        var rule = System.Text.Json.JsonSerializer.Deserialize<FormatRule>(json, options);
        if (rule == null)
            throw new InvalidDataException("JSON 解析失败");

        var root = new StructureNode
        {
            Name = rule.Format,
            Offset = 0,
            Length = 0,
            DataType = FieldDataType.Struct,
            Confidence = -1,
            Source = StructureNodeSource.UserCreated,
            Description = rule.Description,
        };

        foreach (var structDef in rule.Structures)
        {
            var structNode = ConvertFieldsToNode(structDef.Fields, structDef.Name);
            if (structNode != null)
                root.AddChild(structNode);
        }

        // 计算总长度
        if (root.Children.Count > 0)
        {
            var last = root.Children.MaxBy(c => c.Offset + c.Length);
            if (last != null)
                root.Length = last.Offset + last.Length;
        }

        return root;
    }

    private static StructureNode? ConvertFieldsToNode(List<FormatField> fields, string name)
    {
        if (fields.Count == 0) return null;

        var first = fields[0];
        var last = fields[^1];
        var firstOff = first.Offset;
        var lastEnd = last.Offset + (last.Length ?? 4);

        var node = new StructureNode
        {
            Name = name,
            Offset = firstOff,
            Length = lastEnd - firstOff,
            DataType = FieldDataType.Struct,
            Confidence = 1.0,
            Source = StructureNodeSource.UserCreated,
        };

        foreach (var fieldDef in fields)
        {
            var child = new StructureNode
            {
                Name = fieldDef.Name,
                Offset = fieldDef.Offset,
                Length = fieldDef.Length ?? 4,
                DataType = RuleTypeToFieldDataType(fieldDef.Type),
                Endianness = fieldDef.Endianness == "BigEndian" ? FieldEndianness.BigEndian : FieldEndianness.LittleEndian,
                Confidence = 1.0,
                Source = StructureNodeSource.UserCreated,
            };
            node.AddChild(child);
        }

        return node;
    }

    private static FieldDataType RuleTypeToFieldDataType(string type) => type.ToLowerInvariant() switch
    {
        "uint8" => FieldDataType.UInt8,
        "int8" => FieldDataType.Int8,
        "uint16" => FieldDataType.UInt16LE,
        "int16" => FieldDataType.Int16LE,
        "uint32" => FieldDataType.UInt32LE,
        "int32" => FieldDataType.Int32LE,
        "uint64" => FieldDataType.UInt64LE,
        "int64" => FieldDataType.Int64LE,
        "float" => FieldDataType.FloatLE,
        "double" => FieldDataType.DoubleLE,
        "ascii" => FieldDataType.ASCII,
        "utf8" => FieldDataType.UTF8,
        "bytes" => FieldDataType.Bytes,
        "struct" => FieldDataType.Struct,
        _ => FieldDataType.Bytes,
    };
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
