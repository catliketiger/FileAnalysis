using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService.Commands;

public class DeleteFieldCommand : IUndoableCommand
{
    private readonly StructureNode _node;
    private readonly StructureNode _parent;
    private readonly int _childIndex;
    private readonly string _nodeName;

    public string Description => $"删除字段 '{_node.Name}'";

    public DeleteFieldCommand(StructureNode node)
    {
        _node = node;
        _parent = node.Parent ?? throw new InvalidOperationException("根节点不能删除");
        _childIndex = _parent.Children.IndexOf(node);
        _nodeName = node.Name;
    }

    public Task ExecuteAsync()
    {
        _parent.RemoveChild(_node);
        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        _parent.InsertChild(_childIndex, _node);
        return Task.CompletedTask;
    }
}
