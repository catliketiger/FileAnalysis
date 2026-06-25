using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService.Commands;

public class AddFieldCommand : IUndoableCommand
{
    private readonly StructureNode _parent;
    private readonly StructureNode _node;

    public string Description => $"添加字段 '{_node.Name}'";

    public AddFieldCommand(StructureNode parent, StructureNode node)
    {
        _parent = parent;
        _node = node;
    }

    public Task ExecuteAsync()
    {
        _parent.AddChild(_node);
        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        _parent.RemoveChild(_node);
        return Task.CompletedTask;
    }
}
