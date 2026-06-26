using System.Text.Json.Serialization;

namespace FileStruct.Core.Models;

/// <summary>
/// StructureNode 的可序列化 DTO，避免递归类型序列化问题
/// </summary>
public class StructureNodeData
{
    public string Name { get; set; } = "unnamed";
    public string? OriginalName { get; set; }
    public long Offset { get; set; }
    public long Length { get; set; }
    public FieldDataType DataType { get; set; } = FieldDataType.Bytes;
    public FieldEndianness Endianness { get; set; } = FieldEndianness.LittleEndian;
    public double Confidence { get; set; } = -1;
    public string? Description { get; set; }
    public StructureNodeSource Source { get; set; } = StructureNodeSource.AutoDetected;
    public bool IsExpanded { get; set; } = true;
    public string HighlightColor { get; set; } = "#FFFFEB3B";
    public List<StructureNodeData> Children { get; set; } = new();

    /// <summary>从 StructureNode 转换</summary>
    public static StructureNodeData FromNode(StructureNode node) => new()
    {
        Name = node.Name,
        OriginalName = node.OriginalName,
        Offset = node.Offset,
        Length = node.Length,
        DataType = node.DataType,
        Endianness = node.Endianness,
        Confidence = node.Confidence,
        Description = node.Description,
        Source = node.Source,
        IsExpanded = node.IsExpanded,
        HighlightColor = node.HighlightColor,
        Children = node.Children.Select(FromNode).ToList(),
    };

    /// <summary>还原为 StructureNode</summary>
    public StructureNode ToNode()
    {
        var node = new StructureNode
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
            IsExpanded = IsExpanded,
            HighlightColor = HighlightColor,
        };
        foreach (var child in Children)
        {
            var childNode = child.ToNode();
            childNode.Parent = node;
            node.AddChild(childNode);
        }
        return node;
    }
}
