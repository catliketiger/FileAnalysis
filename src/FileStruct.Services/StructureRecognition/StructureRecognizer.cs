using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class StructureRecognizer : IStructureRecognizer
{
    private readonly ISignatureMatcher _signatureMatcher;
    private readonly IHeuristicEngine _heuristicEngine;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogService _logger;

    public StructureRecognizer(
        ISignatureMatcher signatureMatcher,
        IHeuristicEngine heuristicEngine,
        IConfidenceScorer confidenceScorer,
        IRuleEngine ruleEngine,
        ILogService logger)
    {
        _signatureMatcher = signatureMatcher;
        _heuristicEngine = heuristicEngine;
        _confidenceScorer = confidenceScorer;
        _ruleEngine = ruleEngine;
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

            // 尝试加载预置结构定义
            var formatName = bestMatch.Definition.FormatName;
            var matchOffset = bestMatch.MatchOffset;
            var matchedRule = _ruleEngine.GetAllRules()
                .FirstOrDefault(r => string.Equals(r.Format, formatName, StringComparison.OrdinalIgnoreCase));

            if (matchedRule != null && matchedRule.Structures.Count > 0)
            {
                // 使用预置结构构建字段节点
                foreach (var structDef in matchedRule.Structures)
                {
                    var structNode = new StructureNode
                    {
                        Name = structDef.Name,
                        Offset = 0,
                        Length = buffer.Length,
                        DataType = FieldDataType.Struct,
                        Confidence = bestMatch.Score,
                        Source = StructureNodeSource.AutoDetected,
                    };

                    foreach (var fieldDef in structDef.Fields)
                    {
                        var fieldNode = new StructureNode
                        {
                            Name = fieldDef.Name,
                            Offset = fieldDef.Offset,
                            Length = fieldDef.Length ?? GuessFieldLength(fieldDef.Type),
                            DataType = ParseFieldType(fieldDef.Type),
                            Endianness = fieldDef.Endianness == "BigEndian"
                                ? FieldEndianness.BigEndian
                                : FieldEndianness.LittleEndian,
                            Confidence = 0.9,
                            Source = StructureNodeSource.AutoDetected,
                        };

                        // 跳过位置未知的字段（负偏移表示从尾部算，暂不支持）
                        if (fieldNode.Offset >= 0 && fieldNode.Offset + fieldNode.Length <= buffer.Length)
                            structNode.AddChild(fieldNode);
                    }

                    if (structNode.Children.Count > 0)
                        formatNode.AddChild(structNode);
                }
            }

            // 如果没有预置结构或预置结构为空，至少添加魔数字段
            if (formatNode.Children.Count == 0)
            {
                var magicLen = bestMatch.Definition.MagicBytes.Length;
                formatNode.AddChild(new StructureNode
                {
                    Name = "Magic",
                    Offset = matchOffset,
                    Length = magicLen,
                    DataType = FieldDataType.Bytes,
                    Confidence = 1.0,
                    Source = StructureNodeSource.AutoDetected,
                    Description = $"魔数: {BitConverter.ToString(bestMatch.Definition.MagicBytes)}",
                });
            }

            root.AddChild(formatNode);
        }
        else
        {
            _logger.Info("签名匹配无结果，进入启发式推断");
            // Stage 2: 启发式推断（仅当签名未匹配时）
            progress?.Report(new RecognitionProgress(50, "正在进行启发式推断..."));
            var heuristicResult = await _heuristicEngine.InferAsync(buffer, progress, ct);
            foreach (var child in heuristicResult.Children)
                root.AddChild(child);
        }

        ct.ThrowIfCancellationRequested();

        // Stage 3: 置信度计算
        progress?.Report(new RecognitionProgress(90, "正在计算置信度..."));
        foreach (var match in signatureMatches)
        {
            var formatNode = root.Children.FirstOrDefault(c => c.Name == match.Definition.FormatName);
            if (formatNode != null)
                _confidenceScorer.Calculate(formatNode, match);
        }
        _confidenceScorer.Propagate(root);

        progress?.Report(new RecognitionProgress(100, "结构识别完成"));
        _logger.Info($"结构识别完成，共 {CountNodes(root)} 个节点");

        return root;
    }

    private static int GuessFieldLength(string type) => type.ToLowerInvariant() switch
    {
        "uint8" or "int8" or "ascii" => 1,
        "uint16" or "int16" => 2,
        "uint32" or "int32" or "float" => 4,
        "uint64" or "int64" or "double" => 8,
        _ => 4,
    };

    private static FieldDataType ParseFieldType(string type) => type.ToLowerInvariant() switch
    {
        "uint8" => FieldDataType.UInt8,
        "int8" => FieldDataType.Int8,
        "uint16" => FieldDataType.UInt16LE,
        "int16" => FieldDataType.Int16LE,
        "uint32" => FieldDataType.UInt32LE,
        "int32" => FieldDataType.Int32LE,
        "uint64" => FieldDataType.UInt64LE,
        "int64" => FieldDataType.Int64LE,
        "float" => FieldDataType.FloatLE,
        "double" => FieldDataType.DoubleLE,
        "ascii" => FieldDataType.ASCII,
        "utf8" => FieldDataType.UTF8,
        "bytes" => FieldDataType.Bytes,
        _ => FieldDataType.Bytes,
    };

    private static int CountNodes(StructureNode node) => 1 + node.Children.Sum(CountNodes);
}
