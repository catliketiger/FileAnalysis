using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileStruct.Core.Models;

/// <summary>
/// 结构树节点模型 — V1.0 的中心数据模型
/// 表示文件中的一个结构字段/区块，以递归树形式组织
/// </summary>
public class StructureNode : INotifyPropertyChanged
{
    private string _name = "unnamed";
    private long _offset;
    private long _length;
    private FieldDataType _dataType = FieldDataType.Bytes;
    private FieldEndianness _endianness = FieldEndianness.LittleEndian;
    private double _confidence = -1;
    private string? _description;
    private StructureNodeSource _source = StructureNodeSource.AutoDetected;
    private bool _isExpanded = true;
    private bool _isSelected;
    private bool _isHighlighted;
    private string _highlightColor = "#FFFFEB3B";

    public Guid Id { get; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? OriginalName { get; set; }

    public long Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value);
    }

    public long Length
    {
        get => _length;
        set => SetProperty(ref _length, value);
    }

    public FieldDataType DataType
    {
        get => _dataType;
        set => SetProperty(ref _dataType, value);
    }

    public FieldEndianness Endianness
    {
        get => _endianness;
        set => SetProperty(ref _endianness, value);
    }

    public double Confidence
    {
        get => _confidence;
        set
        {
            if (SetProperty(ref _confidence, value))
                OnPropertyChanged(nameof(IsConfidenceAvailable));
        }
    }

    public bool IsConfidenceAvailable => _confidence >= 0;

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public StructureNodeSource Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    /// <summary>该字段/条目是否加密（用于压缩包和文档加密提示）</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>是否分卷占位节点（未加载的分卷文件，不随 JSON 导入导出）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsVolumePlaceholder { get; set; }

    public string HighlightColor
    {
        get => _highlightColor;
        set => SetProperty(ref _highlightColor, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public StructureNode? Parent { get; set; }

    public List<StructureNode> Children { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public StructureNode? OriginalSnapshot { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLeaf => Children.Count == 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRoot => Parent == null;

    [System.Text.Json.Serialization.JsonIgnore]
    public string PathName => Parent == null ? Name : $"{Parent.PathName}/{Name}";

    /// <summary>反序列化后重建 Parent 引用链</summary>
    public void RebuildParentReferences()
    {
        foreach (var child in Children)
        {
            child.Parent = this;
            child.RebuildParentReferences();
        }
    }

    public void AddChild(StructureNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void InsertChild(int index, StructureNode child)
    {
        child.Parent = this;
        Children.Insert(index, child);
    }

    public bool RemoveChild(StructureNode child)
    {
        child.Parent = null;
        return Children.Remove(child);
    }

    public StructureNode? FindByOffset(long offset)
    {
        if (offset >= Offset && offset < Offset + Length)
        {
            foreach (var child in Children)
            {
                var found = child.FindByOffset(offset);
                if (found != null) return found;
            }
            return this;
        }
        return null;
    }

    public StructureNode Snapshot()
    {
        var snap = new StructureNode
        {
            Name = Name,
            OriginalName = OriginalName,
            Offset = Offset,
            Length = Length,
            DataType = DataType,
            Endianness = Endianness,
            Confidence = Confidence,
            Description = Description,
            Source = Source,
            HighlightColor = HighlightColor,
            IsEncrypted = IsEncrypted,
            IsVolumePlaceholder = IsVolumePlaceholder,
            OriginalSnapshot = OriginalSnapshot,
        };
        foreach (var child in Children)
        {
            snap.AddChild(child.Snapshot());
        }
        return snap;
    }

    public void RestoreFrom(StructureNode snapshot)
    {
        Name = snapshot.Name;
        OriginalName = snapshot.OriginalName;
        Offset = snapshot.Offset;
        Length = snapshot.Length;
        DataType = snapshot.DataType;
        Endianness = snapshot.Endianness;
        Confidence = snapshot.Confidence;
        Description = snapshot.Description;
        Source = snapshot.Source;
        HighlightColor = snapshot.HighlightColor;
        IsEncrypted = snapshot.IsEncrypted;
        IsVolumePlaceholder = snapshot.IsVolumePlaceholder;

        Children.Clear();
        foreach (var child in snapshot.Children)
        {
            var restored = new StructureNode();
            restored.RestoreFrom(child);
            AddChild(restored);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
