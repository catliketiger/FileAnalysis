using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IStructureRecognizer
{
    Task<StructureNode> RecognizeAsync(BinaryBuffer buffer,
        IProgress<RecognitionProgress>? progress = null,
        CancellationToken ct = default);

    StructureNode Recognize(BinaryBuffer buffer);
}
