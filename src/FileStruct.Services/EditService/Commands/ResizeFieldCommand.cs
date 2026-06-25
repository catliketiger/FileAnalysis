using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService.Commands;

public class ResizeFieldCommand : IUndoableCommand
{
    private readonly StructureNode _node;
    private readonly long _originalLength;
    private readonly long _newLength;

    public string Description => $"调整字段 '{_node.Name}' 长度: {_originalLength}→{_newLength}";

    public ResizeFieldCommand(StructureNode node, long newLength)
    {
        _node = node;
        _originalLength = node.Length;
        _newLength = newLength;

        if (node.Source == StructureNodeSource.AutoDetected)
            node.Source = StructureNodeSource.UserModified;
    }

    public Task ExecuteAsync()
    {
        _node.Length = _newLength;
        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        _node.Length = _originalLength;
        return Task.CompletedTask;
    }
}
