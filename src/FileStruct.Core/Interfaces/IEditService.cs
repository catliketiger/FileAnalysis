using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IEditService
{
    StructureNode AddField(StructureNode parent, string name, long offset, long length,
        FieldDataType dataType = FieldDataType.Bytes);

    bool DeleteField(StructureNode node);

    bool ModifyField(StructureNode node, string? newName = null, long? newOffset = null,
        long? newLength = null, FieldDataType? newDataType = null,
        FieldEndianness? newEndianness = null);

    bool ResizeField(StructureNode node, long newLength);

    bool ResetField(StructureNode node);
}
