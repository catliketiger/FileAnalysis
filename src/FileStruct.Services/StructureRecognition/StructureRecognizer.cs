using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class StructureRecognizer : IStructureRecognizer
{
    private readonly ISignatureMatcher _signatureMatcher;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogService _logger;

    public StructureRecognizer(
        ISignatureMatcher signatureMatcher,
        IConfidenceScorer confidenceScorer,
        IRuleEngine ruleEngine,
        ILogService logger)
    {
        _signatureMatcher = signatureMatcher;
        _confidenceScorer = confidenceScorer;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    public StructureNode Recognize(BinaryBuffer buffer)
    {
        return RecognizeAsync(buffer).GetAwaiter().GetResult();
    }

    public Task<StructureNode> RecognizeAsync(BinaryBuffer buffer,
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

        // 读取足够大的头部用于魔数匹配（覆盖ISO@0x8001等远偏移签名）
        var headerSize = (int)Math.Min(buffer.Length, 65536);
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
                // 处理动态偏移格式（如 PE 需读取 e_lfanew 定位 COFF 头部）
                var baseOffset = GetDynamicBaseOffset(formatName, buffer);

                // 使用预置结构构建字段节点
                foreach (var structDef in matchedRule.Structures)
                {
                    // Repeating 模式：按固定步长重复同一结构
                    var repeatBase = baseOffset + structDef.BaseRepeatOffset;
                    var repeatCount = 1;
                    if (structDef.Repeating)
                    {
                        repeatCount = structDef.FixedCount ?? 1;
                        // CountField：从文件读取动态重复次数
                        if (structDef.CountField != null)
                        {
                            foreach (var fd in matchedRule.Structures.SelectMany(s => s.Fields))
                            {
                                if (fd.Name == structDef.CountField)
                                {
                                    var cfOff = fd.Offset + baseOffset;
                                    if (cfOff + 2 <= buffer.Length)
                                    {
                                        var cv = (int)buffer.ReadUInt16(cfOff, true);
                                        if (cv > 0 && cv < 65536) repeatCount = cv;
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    for (int rep = 0; rep < repeatCount; rep++)
                    {
                        var repStep = structDef.Repeating ? rep * structDef.StepSize : 0;
                        var structNode = new StructureNode
                        {
                            Name = structDef.Repeating ? $"{structDef.Name}[{rep}]" : structDef.Name,
                            Offset = 0, Length = buffer.Length,
                            DataType = FieldDataType.Struct,
                            Confidence = bestMatch.Score,
                            Source = StructureNodeSource.AutoDetected,
                        };

                        long currentPos = repeatBase + repStep;

                        foreach (var fieldDef in structDef.Fields)
                        {
                            var fieldBase = structDef.Repeating ? repeatBase + repStep : baseOffset;
                            long realOffset;

                            if (structDef.Sequential && fieldDef.Offset < 0)
                                realOffset = currentPos;
                            else
                            {
                                realOffset = fieldDef.Offset + fieldBase;
                                if (structDef.Sequential) currentPos = realOffset;
                            }

                            // offsetFrom：从指定字段的值获取偏移增量
                            if (fieldDef.OffsetFromField != null)
                            {
                                var refOff = fieldDef.Offset + fieldBase;
                                if (refOff + 4 <= buffer.Length)
                                    realOffset = buffer.ReadUInt32(refOff, true);
                            }

                            var fieldLen = fieldDef.Length ?? GuessFieldLength(fieldDef.Type);

                            // lengthField：从指定字段的值获取长度
                            if (fieldDef.LengthField != null)
                            {
                                var lenOff = fieldDef.Offset + fieldBase;
                                if (lenOff + 4 <= buffer.Length)
                                    fieldLen = (int)buffer.ReadUInt32(lenOff, true);
                            }

                            if (structDef.Sequential) currentPos += fieldLen;

                            var fieldNode = new StructureNode
                            {
                                Name = fieldDef.Name,
                                Offset = realOffset, Length = fieldLen,
                                DataType = ParseFieldType(fieldDef.Type),
                                Endianness = fieldDef.Endianness == "BigEndian"
                                    ? FieldEndianness.BigEndian : FieldEndianness.LittleEndian,
                                Confidence = 0.9, Source = StructureNodeSource.AutoDetected,
                            };
                            if (fieldNode.Offset >= 0 && fieldNode.Offset + fieldNode.Length <= buffer.Length)
                                structNode.AddChild(fieldNode);
                        }
                        if (structNode.Children.Count > 0)
                            formatNode.AddChild(structNode);
                    }
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
            _logger.Info("签名匹配无结果，显示 Unknown 根节点");
            progress?.Report(new RecognitionProgress(50, "未识别到已知格式..."));
            root.AddChild(new StructureNode
            {
                Name = "Unknown",
                Offset = 0,
                Length = buffer.Length,
                DataType = FieldDataType.Bytes,
                Confidence = -1,
                Source = StructureNodeSource.AutoDetected,
                Description = "未能自动识别文件格式，可手动添加字段",
            });
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

        // 格式特定的后处理
        if (signatureMatches.Count > 0 && string.Equals(signatureMatches[0].Definition.FormatName, "PDF", StringComparison.OrdinalIgnoreCase))
        {
            var pdfNode = root.Children.FirstOrDefault(c => c.Name == "PDF");
            if (pdfNode != null)
                PostProcessPdf(buffer, pdfNode);
        }

        progress?.Report(new RecognitionProgress(100, "结构识别完成"));
        _logger.Info($"结构识别完成，共 {CountNodes(root)} 个节点");

        return Task.FromResult(root);
    }

    /// <summary>
    /// PDF 后处理：从文件尾扫描 xref 表和 trailer，添加结构节点
    /// </summary>
    private static void PostProcessPdf(BinaryBuffer buffer, StructureNode pdfNode)
    {
        try
        {
            // 扫描文件末尾查找 %%EOF（PDF 标准规定最后一行）
            var tailSize = (int)Math.Min(buffer.Length, 1024);
            var tailOffset = buffer.Length - tailSize;
            var tail = buffer.ReadBytes(tailOffset, tailSize);
            var tailStr = System.Text.Encoding.ASCII.GetString(tail);

            // 查找最后一个 %%EOF
            var eofIdx = tailStr.LastIndexOf("%%EOF", StringComparison.Ordinal);
            if (eofIdx < 0) return;

            // 向前查找 startxref（在 %%EOF 之前两行）
            var beforeEof = tailStr[..eofIdx];
            var lines = beforeEof.Split('\n', '\r');
            string? startxrefLine = null;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && long.TryParse(trimmed, out _))
                {
                    startxrefLine = trimmed;
                    break;
                }
            }
            if (startxrefLine == null) return;
            var xrefOffset = long.Parse(startxrefLine);
            if (xrefOffset < 0 || xrefOffset >= buffer.Length) return;

            // 读取 xref 表
            var xrefSize = (int)Math.Min(buffer.Length - xrefOffset, 4096);
            var xrefData = buffer.ReadBytes(xrefOffset, xrefSize);
            var xrefStr = System.Text.Encoding.ASCII.GetString(xrefData);

            // 解析 xref 头部："xref\r\n" 或 "xref\n"
            if (!xrefStr.StartsWith("xref", StringComparison.Ordinal)) return;
            var xrefLines = xrefStr.Split('\n', '\r')
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

            // 解析 xref 子段: "startObjId objCount"
            int totalObjects = 0;
            int iLine = 0;
            while (iLine < xrefLines.Length)
            {
                var parts = xrefLines[iLine].Split(' ');
                if (parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out var count))
                {
                    totalObjects += count;
                    iLine++;
                    // 跳过 entry 行
                    for (int e = 0; e < count && iLine < xrefLines.Length; e++)
                        iLine++;
                }
                else if (xrefLines[iLine] == "xref")
                {
                    iLine++;
                }
                else break; // 到达 trailer
            }

            // 查找 trailer
            var trailerIdx = xrefStr.IndexOf("trailer", StringComparison.Ordinal);
            if (trailerIdx < 0) return;
            var trailerPart = xrefStr[trailerIdx..];

            // 添加 xref 节点
            pdfNode.AddChild(new StructureNode
            {
                Name = $"Xref Table ({totalObjects} objects)",
                Offset = xrefOffset,
                Length = trailerIdx,
                DataType = FieldDataType.Bytes,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
                Description = $"交叉引用表，共 {totalObjects} 个对象",
            });

            // 解析 trailer 字典
            var trailerNode = new StructureNode
            {
                Name = "Trailer",
                Offset = xrefOffset + trailerIdx,
                Length = trailerPart.Length,
                DataType = FieldDataType.Struct,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
            };

            // 提取关键字典条目
            AddTrailerEntry(trailerNode, trailerPart, "/Size", "对象总数");
            AddTrailerEntry(trailerNode, trailerPart, "/Root", "根对象引用");
            AddTrailerEntry(trailerNode, trailerPart, "/Info", "文档信息引用");
            AddTrailerEntry(trailerNode, trailerPart, "/Pages", "页面目录引用");

            if (trailerNode.Children.Count > 0)
                pdfNode.AddChild(trailerNode);
        }
        catch (Exception ex)
        {
            // PDF 解析失败不中断识别过程
            System.Diagnostics.Debug.WriteLine($"[PDF] 后处理异常: {ex.Message}");
        }
    }

    private static void AddTrailerEntry(StructureNode parent, string trailer, string key, string label)
    {
        var idx = trailer.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return;

        // 提取键后的值（直到遇到 >> 或下一个 /）
        var valueStart = idx + key.Length;
        var valueEnd = valueStart;
        while (valueEnd < trailer.Length && trailer[valueEnd] != '>' && trailer[valueEnd] != '/')
            valueEnd++;
        if (valueEnd == valueStart) return;

        var value = trailer[valueStart..valueEnd].Trim();
        if (value.Length > 40) value = value[..40] + "...";

        parent.AddChild(new StructureNode
        {
            Name = $"{key} ({label})",
            Offset = parent.Offset + idx,
            Length = valueEnd - idx,
            DataType = FieldDataType.ASCII,
            Confidence = 0.8,
            Source = StructureNodeSource.AutoDetected,
            Description = value,
        });
    }

    /// <summary>获取动态偏移格式的基址偏移量</summary>
    private static long GetDynamicBaseOffset(string formatName, BinaryBuffer buffer)
    {
        if (string.Equals(formatName, "PE", StringComparison.OrdinalIgnoreCase))
        {
            // PE 格式：e_lfanew 在 DOS Header 偏移 60 处
            if (buffer.Length >= 64)
            {
                var eLfanew = buffer.ReadUInt32(60, true);
                // PE signature at e_lfanew, COFF header at e_lfanew + 4
                return eLfanew + 4; // 返回 COFF 头部的起始偏移
            }
        }
        return 0;
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
