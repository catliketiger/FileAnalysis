using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IConfidenceScorer
{
    double Calculate(StructureNode node, SignatureMatch? match = null);
    void Propagate(StructureNode root);
}
