using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService.Commands;

public class ResetFieldCommand : IUndoableCommand
{
    private readonly StructureNode _node;
    private readonly string _originalName;
    private readonly long _originalOffset;
    private readonly long _originalLength;
    private readonly FieldDataType _originalDataType;
    private readonly StructureNodeSource _originalSource;

    public string Description => $"重置字段 '{_node.Name}' 为原始状态";

    public ResetFieldCommand(StructureNode node)
    {
        _node = node;
        _originalName = node.Name;
        _originalOffset = node.Offset;
        _originalLength = node.Length;
        _originalDataType = node.DataType;
        _originalSource = node.Source;
    }

    public Task ExecuteAsync()
    {
        if (_node.OriginalSnapshot != null)
        {
            _node.RestoreFrom(_node.OriginalSnapshot);
            _node.Source = StructureNodeSource.UserReset;
        }
        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        _node.Name = _originalName;
        _node.Offset = _originalOffset;
        _node.Length = _originalLength;
        _node.DataType = _originalDataType;
        _node.Source = _originalSource;
        return Task.CompletedTask;
    }
}
