using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IHeuristicEngine
{
    Task<StructureNode> InferAsync(BinaryBuffer buffer,
        IProgress<RecognitionProgress>? progress = null,
        CancellationToken ct = default);
}
