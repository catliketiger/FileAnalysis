using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class ConfidenceScorer : IConfidenceScorer
{
    public double Calculate(StructureNode node, SignatureMatch? match = null)
    {
        if (node.Source == StructureNodeSource.UserCreated)
            return 1.0;

        if (match.HasValue)
        {
            // 基于签名匹配的置信度
            var baseScore = match.Value.Score;
            var lengthBonus = Math.Min(0.2, node.Length / 1000.0 * 0.1);
            return Math.Min(1.0, baseScore + lengthBonus);
        }

        // 启发式推断的置信度
        double confidence = 0.3;

        if (node.Length > 0) confidence += 0.1;
        if (!string.IsNullOrEmpty(node.Name) && node.Name != "unnamed") confidence += 0.1;

        return Math.Min(1.0, confidence);
    }

    public void Propagate(StructureNode root)
    {
        PropagateInternal(root);
    }

    private static double PropagateInternal(StructureNode node)
    {
        if (node.IsLeaf) return node.Confidence;

        double sum = 0;
        foreach (var child in node.Children)
            sum += PropagateInternal(child);

        node.Confidence = sum / node.Children.Count;
        return node.Confidence;
    }
}
