using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class StructureRecognizer : IStructureRecognizer
{
    private readonly ISignatureMatcher _signatureMatcher;
    private readonly IHeuristicEngine _heuristicEngine;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly ILogService _logger;

    public StructureRecognizer(
        ISignatureMatcher signatureMatcher,
        IHeuristicEngine heuristicEngine,
        IConfidenceScorer confidenceScorer,
        ILogService logger)
    {
        _signatureMatcher = signatureMatcher;
        _heuristicEngine = heuristicEngine;
        _confidenceScorer = confidenceScorer;
        _logger = logger;
    }

    public StructureNode Recognize(BinaryBuffer buffer)
    {
        return RecognizeAsync(buffer).GetAwaiter().GetResult();
    }

    public async Task<StructureNode> RecognizeAsync(BinaryBuffer buffer,
        IProgress<RecognitionProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var op = _logger.BeginOperation("结构识别");

        var root = new StructureNode
        {
            Name = "文件根",
            Offset = 0,
            Length = buffer.Length,
            DataType = FieldDataType.Bytes,
            Confidence = 1.0,
            Source = StructureNodeSource.AutoDetected,
        };

        progress?.Report(new RecognitionProgress(5, "开始结构识别..."));

        // Stage 1: 魔数匹配
        _logger.Debug("Stage 1: 魔数匹配");
        progress?.Report(new RecognitionProgress(15, "正在进行魔数匹配..."));

        var headerSize = (int)Math.Min(buffer.Length, 1024);
        var headerBytes = buffer.ReadBytes(0, headerSize);
        var signatureMatches = _signatureMatcher.Match(headerBytes);

        if (signatureMatches.Count > 0)
        {
            var bestMatch = signatureMatches[0];
            _logger.Info($"签名匹配成功: {bestMatch.Definition.FormatName} (置信度: {bestMatch.Score:P0})");

            var formatNode = new StructureNode
            {
                Name = bestMatch.Definition.FormatName,
                Offset = 0,
                Length = buffer.Length,
                DataType = FieldDataType.Struct,
                Confidence = bestMatch.Score,
                Source = StructureNodeSource.AutoDetected,
                Description = bestMatch.Definition.Description,
            };

            // 添加魔数字段
            var magicLen = bestMatch.Definition.MagicBytes.Length;
            var magicField = new StructureNode
            {
                Name = "Magic",
                Offset = bestMatch.MatchOffset,
                Length = magicLen,
                DataType = FieldDataType.Bytes,
                Confidence = 1.0,
                Source = StructureNodeSource.AutoDetected,
                Description = $"魔数: {BitConverter.ToString(bestMatch.Definition.MagicBytes)}",
            };
            formatNode.AddChild(magicField);

            root.AddChild(formatNode);
        }
        else
        {
            _logger.Info("签名匹配无结果，进入启发式推断");
        }

        ct.ThrowIfCancellationRequested();

        // Stage 2: 启发式推断
        _logger.Debug("Stage 2: 启发式推断");
        progress?.Report(new RecognitionProgress(50, "正在进行启发式推断..."));

        var heuristicResult = await _heuristicEngine.InferAsync(buffer, progress, ct);

        // 将启发式结果合并到根节点
        foreach (var child in heuristicResult.Children)
        {
            root.AddChild(child);
        }

        // Stage 3: 置信度计算
        _logger.Debug("Stage 3: 置信度传播");
        progress?.Report(new RecognitionProgress(90, "正在计算置信度..."));

        foreach (var match in signatureMatches)
        {
            // 为匹配到的签名节点设置置信度
            var formatNode = root.Children.FirstOrDefault(c => c.Name == match.Definition.FormatName);
            if (formatNode != null)
            {
                _confidenceScorer.Calculate(formatNode, match);
            }
        }
        _confidenceScorer.Propagate(root);

        progress?.Report(new RecognitionProgress(100, "结构识别完成"));
        _logger.Info($"结构识别完成，共 {CountNodes(root)} 个节点");

        return root;
    }

    private static int CountNodes(StructureNode node)
    {
        return 1 + node.Children.Sum(CountNodes);
    }
}
