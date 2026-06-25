using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService;

public class EditService : IEditService
{
    private readonly IUndoRedoService _undoRedo;

    public EditService(IUndoRedoService undoRedo)
    {
        _undoRedo = undoRedo;
    }

    public StructureNode AddField(StructureNode parent, string name, long offset, long length,
        FieldDataType dataType = FieldDataType.Bytes)
    {
        var node = new StructureNode
        {
            Name = name,
            Offset = offset,
            Length = length,
            DataType = dataType,
            Source = StructureNodeSource.UserCreated,
            Confidence = 1.0,
        };
        var cmd = new Commands.AddFieldCommand(parent, node);
        _undoRedo.ExecuteAsync(cmd).GetAwaiter().GetResult();
        return node;
    }

    public bool DeleteField(StructureNode node)
    {
        if (node.Parent == null) return false;
        var cmd = new Commands.DeleteFieldCommand(node);
        _undoRedo.ExecuteAsync(cmd).GetAwaiter().GetResult();
        return true;
    }

    public bool ModifyField(StructureNode node, string? newName = null,
        long? newOffset = null, long? newLength = null,
        FieldDataType? newDataType = null, FieldEndianness? newEndianness = null)
    {
        var cmd = new Commands.ModifyFieldCommand(node, newName, newOffset,
            newLength, newDataType, newEndianness);
        _undoRedo.ExecuteAsync(cmd).GetAwaiter().GetResult();
        return true;
    }

    public bool ResizeField(StructureNode node, long newLength)
    {
        var cmd = new Commands.ResizeFieldCommand(node, newLength);
        _undoRedo.ExecuteAsync(cmd).GetAwaiter().GetResult();
        return true;
    }

    public bool ResetField(StructureNode node)
    {
        if (node.OriginalSnapshot == null) return false;
        var cmd = new Commands.ResetFieldCommand(node);
        _undoRedo.ExecuteAsync(cmd).GetAwaiter().GetResult();
        return true;
    }
}
