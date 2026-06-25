using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService.Commands;

public class ModifyFieldCommand : IUndoableCommand
{
    private readonly StructureNode _node;
    private readonly string? _originalName;
    private readonly long _originalOffset;
    private readonly long _originalLength;
    private readonly FieldDataType _originalDataType;
    private readonly FieldEndianness _originalEndianness;
    private readonly string? _newName;
    private readonly long? _newOffset;
    private readonly long? _newLength;
    private readonly FieldDataType? _newDataType;
    private readonly FieldEndianness? _newEndianness;

    public string Description
    {
        get
        {
            var parts = new List<string>();
            if (_newName != null) parts.Add($"名称: {_originalName}→{_newName}");
            if (_newLength != null) parts.Add($"长度: {_originalLength}→{_newLength}");
            return $"修改字段 '{_node.Name}': {string.Join(", ", parts)}";
        }
    }

    public ModifyFieldCommand(StructureNode node, string? newName,
        long? newOffset, long? newLength,
        FieldDataType? newDataType, FieldEndianness? newEndianness)
    {
        _node = node;
        _originalName = node.Name;
        _originalOffset = node.Offset;
        _originalLength = node.Length;
        _originalDataType = node.DataType;
        _originalEndianness = node.Endianness;
        _newName = newName;
        _newOffset = newOffset;
        _newLength = newLength;
        _newDataType = newDataType;
        _newEndianness = newEndianness;

        // 跟踪用户修改
        if (node.Source == StructureNodeSource.AutoDetected)
            node.Source = StructureNodeSource.UserModified;
    }

    public Task ExecuteAsync()
    {
        if (_newName != null) _node.Name = _newName;
        if (_newOffset.HasValue) _node.Offset = _newOffset.Value;
        if (_newLength.HasValue) _node.Length = _newLength.Value;
        if (_newDataType.HasValue) _node.DataType = _newDataType.Value;
        if (_newEndianness.HasValue) _node.Endianness = _newEndianness.Value;
        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        _node.Name = _originalName!;
        _node.Offset = _originalOffset;
        _node.Length = _originalLength;
        _node.DataType = _originalDataType;
        _node.Endianness = _originalEndianness;
        return Task.CompletedTask;
    }
}
