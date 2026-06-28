using System.Text;
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

        string formatName = "";
        bool isApk = false;
        bool isEpub = false;
        bool isCrx = false;
        bool isDmg = false;
        bool isPyc = false;
        bool isPak = false;
        bool isCab = false;
        bool is7z = false;
        bool isGzip = false;
        bool isTar = false;
        bool isZip = false;

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

            // 容器格式特殊处理：ZIP 签名 + 特定扩展名 → 覆盖格式名
            formatName = bestMatch.Definition.FormatName;
            var fileExt = Path.GetExtension(buffer.FilePath);
            isApk = ".apk".Equals(fileExt, StringComparison.OrdinalIgnoreCase);
            isEpub = ".epub".Equals(fileExt, StringComparison.OrdinalIgnoreCase);
            isCrx = string.Equals(formatName, "CRX", StringComparison.OrdinalIgnoreCase);
            isDmg = string.Equals(formatName, "DMG", StringComparison.OrdinalIgnoreCase);
            isPyc = string.Equals(formatName, "PYC", StringComparison.OrdinalIgnoreCase);
            isPak = string.Equals(formatName, "PAK", StringComparison.OrdinalIgnoreCase);
            isCab = string.Equals(formatName, "CAB", StringComparison.OrdinalIgnoreCase);
            is7z = string.Equals(formatName, "7z", StringComparison.OrdinalIgnoreCase);
            isGzip = string.Equals(formatName, "GZip", StringComparison.OrdinalIgnoreCase);
            isTar = string.Equals(formatName, "TAR", StringComparison.OrdinalIgnoreCase);
            isZip = string.Equals(formatName, "ZIP", StringComparison.OrdinalIgnoreCase);
            if (isApk && string.Equals(formatName, "ZIP", StringComparison.OrdinalIgnoreCase))
                formatName = "APK";
            else if (isEpub && string.Equals(formatName, "ZIP", StringComparison.OrdinalIgnoreCase))
                formatName = "EPUB";

            var formatNode = new StructureNode
            {
                Name = formatName,
                Offset = 0,
                Length = buffer.Length,
                DataType = FieldDataType.Struct,
                Confidence = bestMatch.Score,
                Source = StructureNodeSource.AutoDetected,
                Description = bestMatch.Definition.Description,
            };

            // 尝试加载预置结构定义
            var matchOffset = bestMatch.MatchOffset;
            var matchedRule = _ruleEngine.GetAllRules()
                .FirstOrDefault(r => string.Equals(r.Format, formatName, StringComparison.OrdinalIgnoreCase));

            // APK 在结构上与 ZIP 相同，若未找到 APK 专用规则则回退使用 ZIP 规则
            if (matchedRule == null && isApk)
            {
                matchedRule = _ruleEngine.GetAllRules()
                    .FirstOrDefault(r => string.Equals(r.Format, "ZIP", StringComparison.OrdinalIgnoreCase));
            }

            // EPUB 在结构上与 ZIP 相同，同样回退使用 ZIP 规则
            if (matchedRule == null && isEpub)
            {
                matchedRule = _ruleEngine.GetAllRules()
                    .FirstOrDefault(r => string.Equals(r.Format, "ZIP", StringComparison.OrdinalIgnoreCase));
            }

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

            // DMG 扩展名回退：扫描尾部 koly 签名确认
            if (".dmg".Equals(Path.GetExtension(buffer.FilePath), StringComparison.OrdinalIgnoreCase) && buffer.Length >= 512)
            {
                var kolyOff = buffer.Length - 512;
                if (buffer.ReadUInt32(kolyOff, true) == 0x796C6F6B) // "koly"
                {
                    var dmgNode = new StructureNode
                    {
                        Name = "DMG",
                        Offset = 0,
                        Length = buffer.Length,
                        DataType = FieldDataType.Struct,
                        Confidence = 0.85,
                        Source = StructureNodeSource.AutoDetected,
                        Description = "macOS 磁盘映像 (UDIF)",
                    };
                    root.AddChild(dmgNode);
                    PostProcessDmg(buffer, dmgNode);
                    _logger.Info("通过 koly 尾部签名识别为 DMG 格式");
                    goto dmgPostProcessDone;
                }
            }

            // ZIP 扩展名回退：扫描尾部 EOCD 签名确认（分卷末卷可能以中央目录开头）
            var zipExt = Path.GetExtension(buffer.FilePath);
            if ((".zip".Equals(zipExt, StringComparison.OrdinalIgnoreCase) ||
                 ".z01".Equals(zipExt, StringComparison.OrdinalIgnoreCase) ||
                 ".z02".Equals(zipExt, StringComparison.OrdinalIgnoreCase)) && buffer.Length >= 22)
            {
                var tailSize = (int)Math.Min(buffer.Length, 0x100FF);
                var tail = buffer.ReadBytes(buffer.Length - tailSize, tailSize);
                long eocdOff = -1;
                for (int i = tail.Length - 22; i >= 0; i--)
                    if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                    { eocdOff = buffer.Length - tailSize + i; break; }

                if (eocdOff >= 0)
                {
                    var zipNode = new StructureNode
                    {
                        Name = "ZIP",
                        Offset = 0, Length = buffer.Length,
                        DataType = FieldDataType.Struct,
                        Confidence = 0.8,
                        Source = StructureNodeSource.AutoDetected,
                        Description = "ZIP 压缩包 (通过尾部 EOCD 识别)",
                    };
                    // 加载 ZIP 规则
                    var zipRule = _ruleEngine.GetAllRules()
                        .FirstOrDefault(r => string.Equals(r.Format, "ZIP", StringComparison.OrdinalIgnoreCase));
                    if (zipRule != null && zipRule.Structures.Count > 0)
                    {
                        foreach (var structDef in zipRule.Structures)
                        {
                            var structNode = new StructureNode
                            {
                                Name = structDef.Name, Offset = 0, Length = buffer.Length,
                                DataType = FieldDataType.Struct, Confidence = 0.8,
                                Source = StructureNodeSource.AutoDetected,
                            };
                            long pos = 0;
                            foreach (var fieldDef in structDef.Fields)
                            {
                                long realOffset = fieldDef.Offset >= 0 ? fieldDef.Offset : pos;
                                var fieldLen = fieldDef.Length ?? 4;
                                if (realOffset + fieldLen <= buffer.Length)
                                    structNode.AddChild(MakeField(fieldDef.Name, null, realOffset, fieldLen, ParseFieldType(fieldDef.Type), 0.85));
                                if (structDef.Sequential) pos = realOffset + fieldLen;
                            }
                            if (structNode.Children.Count > 0)
                                zipNode.AddChild(structNode);
                        }
                    }
                    root.AddChild(zipNode);
                    PostProcessZip(buffer, zipNode);
                    _logger.Info("通过尾部 EOCD 签名识别为 ZIP 格式");
                    goto dmgPostProcessDone;
                }
            }

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
            // APK/EPUB 特殊：节点名已覆盖但签名匹配名为 "ZIP"
            if (formatNode == null && isApk && match.Definition.FormatName == "ZIP")
                formatNode = root.Children.FirstOrDefault(c => c.Name == "APK");
            if (formatNode == null && isEpub && match.Definition.FormatName == "ZIP")
                formatNode = root.Children.FirstOrDefault(c => c.Name == "EPUB");
            if (formatNode != null)
                _confidenceScorer.Calculate(formatNode, match);
        }
        _confidenceScorer.Propagate(root);

        // 格式特定的后处理
        if (signatureMatches.Count > 0)
        {
            if (string.Equals(formatName, "PDF", StringComparison.OrdinalIgnoreCase))
            {
                var pdfNode = root.Children.FirstOrDefault(c => c.Name == "PDF");
                if (pdfNode != null)
                    PostProcessPdf(buffer, pdfNode);
            }
            else if (string.Equals(formatName, "PE", StringComparison.OrdinalIgnoreCase))
            {
                var peNode = root.Children.FirstOrDefault(c => c.Name == "PE");
                if (peNode != null)
                    PostProcessPe(buffer, peNode);
            }
            else if (string.Equals(formatName, "APK", StringComparison.OrdinalIgnoreCase))
            {
                var apkNode = root.Children.FirstOrDefault(c => c.Name == "APK");
                if (apkNode != null)
                    PostProcessApk(buffer, apkNode);
            }
            else if (string.Equals(formatName, "EPUB", StringComparison.OrdinalIgnoreCase))
            {
                var epubNode = root.Children.FirstOrDefault(c => c.Name == "EPUB");
                if (epubNode != null)
                    PostProcessEpub(buffer, epubNode);
            }
            else if (string.Equals(formatName, "MOBI", StringComparison.OrdinalIgnoreCase))
            {
                var mobiNode = root.Children.FirstOrDefault(c => c.Name == "MOBI");
                if (mobiNode != null)
                    PostProcessMobi(buffer, mobiNode);
            }
            else if (string.Equals(formatName, "CRX", StringComparison.OrdinalIgnoreCase))
            {
                var crxNode = root.Children.FirstOrDefault(c => c.Name == "CRX");
                if (crxNode != null)
                    PostProcessCrx(buffer, crxNode);
            }
            else if (string.Equals(formatName, "LNK", StringComparison.OrdinalIgnoreCase))
            {
                var lnkNode = root.Children.FirstOrDefault(c => c.Name == "LNK");
                if (lnkNode != null)
                    PostProcessLnk(buffer, lnkNode);
            }
            else if (string.Equals(formatName, "DMG", StringComparison.OrdinalIgnoreCase))
            {
                var dmgNode = root.Children.FirstOrDefault(c => c.Name == "DMG");
                if (dmgNode != null)
                    PostProcessDmg(buffer, dmgNode);
            }
            else if (string.Equals(formatName, "PYC", StringComparison.OrdinalIgnoreCase))
            {
                var pycNode = root.Children.FirstOrDefault(c => c.Name == "PYC");
                if (pycNode != null)
                    PostProcessPyc(buffer, pycNode);
            }
            else if (string.Equals(formatName, "PAK", StringComparison.OrdinalIgnoreCase))
            {
                var pakNode = root.Children.FirstOrDefault(c => c.Name == "PAK");
                if (pakNode != null)
                    PostProcessPak(buffer, pakNode);
            }
            else if (string.Equals(formatName, "CAB", StringComparison.OrdinalIgnoreCase))
            {
                var cabNode = root.Children.FirstOrDefault(c => c.Name == "CAB");
                if (cabNode != null)
                    PostProcessCab(buffer, cabNode);
            }
            else if (string.Equals(formatName, "7z", StringComparison.OrdinalIgnoreCase))
            {
                var szNode = root.Children.FirstOrDefault(c => c.Name == "7z");
                if (szNode != null)
                    PostProcess7z(buffer, szNode);
            }
            else if (string.Equals(formatName, "TAR", StringComparison.OrdinalIgnoreCase))
            {
                var tarNode = root.Children.FirstOrDefault(c => c.Name == "TAR");
                if (tarNode != null)
                    PostProcessTar(buffer, tarNode);
            }
            else if (string.Equals(formatName, "ZIP", StringComparison.OrdinalIgnoreCase))
            {
                var zipNode = root.Children.FirstOrDefault(c => c.Name == "ZIP");
                if (zipNode != null)
                    PostProcessZip(buffer, zipNode);
            }
            else if (string.Equals(formatName, "GZip", StringComparison.OrdinalIgnoreCase))
            {
                var gzNode = root.Children.FirstOrDefault(c => c.Name == "GZip");
                if (gzNode != null)
                    PostProcessGzip(buffer, gzNode);
            }
        }

        dmgPostProcessDone:
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
            AddTrailerEntry(trailerNode, trailerPart, "/Encrypt", "加密字典引用");

            if (trailerNode.Children.Count > 0)
                pdfNode.AddChild(trailerNode);

            // PDF 加密提示
            if (trailerPart.Contains("/Encrypt", StringComparison.Ordinal))
            {
                pdfNode.AddChild(new StructureNode
                {
                    Name = "🔒 文件已加密",
                    Offset = xrefOffset + trailerIdx + trailerPart.IndexOf("/Encrypt", StringComparison.Ordinal),
                    Length = 20,
                    DataType = FieldDataType.ASCII,
                    Confidence = 0.9,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "该 PDF 文件受密码保护",
                });
            }
        }
        catch { /* PDF 解析失败不中断识别过程 */ }
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

    /// <summary>
    /// PE 后处理：解析节区表（Section Table），基于节名检测常见加壳工具
    /// </summary>
    private static void PostProcessPe(BinaryBuffer buffer, StructureNode peNode)
    {
        try
        {
            // 1. 从 DOS Header 读取 e_lfanew（文件偏移 60）
            if (buffer.Length < 64) return;
            var eLfanew = buffer.ReadUInt32(60, true);
            if (eLfanew < 0x40 || eLfanew >= buffer.Length - 24) return;

            // 2. 读取 COFF File Header 的 NumberOfSections 和 SizeOfOptionalHeader
            var coffOffset = (long)eLfanew + 4;
            if (coffOffset + 20 > buffer.Length) return;

            var numSections = buffer.ReadUInt16(coffOffset + 2, true);
            var sizeOptHeader = buffer.ReadUInt16(coffOffset + 16, true);

            if (numSections < 1 || numSections > 100) return;
            if (sizeOptHeader < 96 || sizeOptHeader > 256) return;

            // 3. 节区表偏移 = e_lfanew + 4 (COFF) + 20 (COFF size) + SizeOfOptionalHeader
            var sectionTableOffset = (long)eLfanew + 24 + sizeOptHeader;

            // 4. 读取入口点地址（Optional Header 中偏移 0x10 处 = e_lfanew + 40）
            var addressOfEntryPoint = 0u;
            if (eLfanew + 44 <= buffer.Length)
                addressOfEntryPoint = buffer.ReadUInt32(eLfanew + 40, true);

            // 5. 解析节区表条目
            var sectionNames = new List<string>();
            var sectionInfos = new List<(uint virtualSize, uint virtualAddress, uint rawSize, uint rawOffset, uint characteristics)>();

            var sectionsNode = new StructureNode
            {
                Name = $"Section Table ({numSections} entries)",
                Offset = sectionTableOffset,
                Length = numSections * 40L,
                DataType = FieldDataType.Struct,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
            };

            for (int i = 0; i < numSections; i++)
            {
                var entryOffset = sectionTableOffset + i * 40L;
                if (entryOffset + 40 > buffer.Length) break;

                var nameBytes = buffer.ReadBytes(entryOffset, 8);
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                // 清理不可打印字符
                name = new string(name.Where(c => c >= 32 && c < 127).ToArray());
                sectionNames.Add(name);

                var virtualSize = buffer.ReadUInt32(entryOffset + 8, true);
                var virtualAddress = buffer.ReadUInt32(entryOffset + 12, true);
                var sizeOfRawData = buffer.ReadUInt32(entryOffset + 16, true);
                var pointerToRawData = buffer.ReadUInt32(entryOffset + 20, true);
                var characteristics = buffer.ReadUInt32(entryOffset + 36, true);

                sectionInfos.Add((virtualSize, virtualAddress, sizeOfRawData, pointerToRawData, characteristics));

                var secNode = new StructureNode
                {
                    Name = string.IsNullOrEmpty(name) ? $"Section[{i}]" : name,
                    Offset = entryOffset,
                    Length = 40,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.9,
                    Source = StructureNodeSource.AutoDetected,
                };

                // 添加子字段
                secNode.AddChild(MakeField("Name", name, entryOffset, 8, FieldDataType.ASCII, 0.95));
                secNode.AddChild(MakeField("VirtualSize", $"0x{virtualSize:X8} ({virtualSize})", entryOffset + 8, 4, FieldDataType.UInt32LE, 0.9));
                secNode.AddChild(MakeField("VirtualAddress", $"0x{virtualAddress:X8}", entryOffset + 12, 4, FieldDataType.UInt32LE, 0.9));
                secNode.AddChild(MakeField("SizeOfRawData", $"0x{sizeOfRawData:X8} ({sizeOfRawData})", entryOffset + 16, 4, FieldDataType.UInt32LE, 0.9));
                secNode.AddChild(MakeField("PointerToRawData", $"0x{pointerToRawData:X8}", entryOffset + 20, 4, FieldDataType.UInt32LE, 0.9));
                secNode.AddChild(MakeField("Characteristics", $"0x{characteristics:X8} ({FormatSectionFlags(characteristics)})", entryOffset + 36, 4, FieldDataType.UInt32LE, 0.9));

                // 添加点/重定位/行号等次级字段（折叠的简洁版本）
                secNode.AddChild(MakeField("PointerToRelocations", $"0x{buffer.ReadUInt32(entryOffset + 24, true):X8}", entryOffset + 24, 4, FieldDataType.UInt32LE, 0.8));
                secNode.AddChild(MakeField("PointerToLinenumbers", $"0x{buffer.ReadUInt32(entryOffset + 28, true):X8}", entryOffset + 28, 4, FieldDataType.UInt32LE, 0.8));
                secNode.AddChild(MakeField("NumberOfRelocations", $"{buffer.ReadUInt16(entryOffset + 32, true)}", entryOffset + 32, 2, FieldDataType.UInt16LE, 0.8));
                secNode.AddChild(MakeField("NumberOfLinenumbers", $"{buffer.ReadUInt16(entryOffset + 34, true)}", entryOffset + 34, 2, FieldDataType.UInt16LE, 0.8));

                sectionsNode.AddChild(secNode);
            }

            if (sectionsNode.Children.Count > 0)
                peNode.AddChild(sectionsNode);

            // 6. 执行加壳检测
            DetectPacker(peNode, sectionNames, sectionInfos, addressOfEntryPoint, sectionTableOffset);
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>快捷创建 StructureNode 字段节点</summary>
    private static StructureNode MakeField(string name, string? description, long offset, long length, FieldDataType type, double confidence)
    {
        return new StructureNode
        {
            Name = name,
            Offset = offset,
            Length = length,
            DataType = type,
            Confidence = confidence,
            Source = StructureNodeSource.AutoDetected,
            Description = description,
        };
    }

    /// <summary>格式化节区特征值标志</summary>
    private static string FormatSectionFlags(uint flags)
    {
        var list = new List<string>();
        if ((flags & 0x00000020) != 0) list.Add("CODE");
        if ((flags & 0x00000040) != 0) list.Add("INIT_DATA");
        if ((flags & 0x00000080) != 0) list.Add("UNINIT_DATA");
        if ((flags & 0x02000000) != 0) list.Add("DISCARDABLE");
        if ((flags & 0x10000000) != 0) list.Add("EXECUTE");
        if ((flags & 0x20000000) != 0) list.Add("READ");
        if ((flags & 0x40000000) != 0) list.Add("WRITE");
        if ((flags & 0x00000010) != 0) list.Add("SHARED");
        return list.Count > 0 ? string.Join(" | ", list) : "NONE";
    }

    /// <summary>
    /// 基于节区名和入口点等特征检测常见可执行文件加壳工具
    /// </summary>
    private static void DetectPacker(StructureNode peNode, List<string> sectionNames,
        List<(uint virtualSize, uint virtualAddress, uint rawSize, uint rawOffset, uint characteristics)> sectionInfos,
        uint entryPoint, long sectionTableOffset)
    {
        var lower = sectionNames.Select(n => n.ToLowerInvariant()).ToList();

        // ——— UPX ——— 最主流的开源压缩壳
        if (lower.Any(n => n is ".upx0" or ".upx1" or "upx0" or "upx1"))
        {
            AddPackerNode(peNode, "UPX", "UPX (Ultimate Packer for eXecutables) — 开源可执行文件压缩壳，广泛用于减小文件体积", sectionTableOffset);
            return;
        }

        // ——— ASPack ——— 商业壳，同时使用 .aspack 和 .adata 两个节
        if (lower.Any(n => n is ".aspack" or "aspack"))
        {
            AddPackerNode(peNode, "ASPack", "ASPack — 商业可执行文件压缩壳，支持加密和压缩保护", sectionTableOffset);
            return;
        }

        // ——— Petite ——— 高压缩比工具
        if (lower.Any(n => n is ".petite" or "petite"))
        {
            AddPackerNode(peNode, "Petite", "Petite — 可执行文件压缩工具，以高压缩比著称", sectionTableOffset);
            return;
        }

        // ——— MPRESS ——— 支持 X64
        if (lower.Any(n => n.Contains(".mpress") || n.Contains("mpress")))
        {
            AddPackerNode(peNode, "MPRESS", "MPRESS (MATCODE) — 可执行文件压缩壳，支持 X86 和 X64", sectionTableOffset);
            return;
        }

        // ——— VMProtect ——— 代码虚拟化保护
        if (lower.Any(n => n.Contains(".vmp") || n.Contains("vmp")))
        {
            AddPackerNode(peNode, "VMProtect", "VMProtect — 商业虚拟化保护壳，将 x86/x64 代码转换为虚拟机指令", sectionTableOffset);
            return;
        }

        // ——— Themida / WinLicense ——— Oreans 商业保护
        if (lower.Any(n => n.Contains(".themida") || n.Contains(".safeweb") || n.Contains(".safedrv") || n.Contains(".safexp")))
        {
            AddPackerNode(peNode, "Themida / WinLicense", "Oreans Themida/WinLicense — 商业级代码虚拟化与授权保护系统", sectionTableOffset);
            return;
        }

        // ——— Enigma Protector
        if (lower.Any(n => n.Contains(".enigma")))
        {
            AddPackerNode(peNode, "Enigma Protector", "Enigma Protector — 可执行文件保护壳，支持授权管理、反调试与加密", sectionTableOffset);
            return;
        }

        // ——— Armadillo ——— 多层保护，使用 .adata / .cdata / .tdata
        if (lower.Any(n => n is ".adata" or ".cdata" or ".tdata"))
        {
            AddPackerNode(peNode, "Armadillo", "Armadillo (Silicon Realms) — 商用软件保护系统，支持多层保护机制", sectionTableOffset);
            return;
        }

        // ——— tElock / Telock
        if (lower.Any(n => n is ".tlock" or ".telock" or ".tls0" or ".tls1"))
        {
            AddPackerNode(peNode, "tElock", "tElock — 免费的 Windows 可执行文件保护工具", sectionTableOffset);
            return;
        }

        // ——— RLPack
        if (lower.Any(n => n.Contains(".rlpack") || n.Contains("rlpack")))
        {
            AddPackerNode(peNode, "RLPack", "RLPack — 可执行文件压缩壳，以快速解压为特点", sectionTableOffset);
            return;
        }

        // ——— EXECryptor
        if (lower.Any(n => n.Contains(".ecrypt") || n.Contains(".execryptor") || n.Contains("ecrypt")))
        {
            AddPackerNode(peNode, "EXECryptor", "EXECryptor (Strongbit) — 商业软件保护壳，支持反调试和反虚拟机检测", sectionTableOffset);
            return;
        }

        // ——— Obsidium
        if (lower.Any(n => n.Contains(".obs") && n is ".obstxt" or ".obsdat"))
        {
            AddPackerNode(peNode, "Obsidium", "Obsidium — 软件授权管理与保护系统", sectionTableOffset);
            return;
        }

        // ——— PEP (Private exe Protector)
        if (lower.Any(n => n.Contains(".pec") && n is ".pec0" or ".pec1" or ".pec2"))
        {
            AddPackerNode(peNode, "PEP (Private exe Protector)", "Private exe Protector — 可执行文件保护与压缩工具", sectionTableOffset);
            return;
        }

        // ——— Safengine
        if (lower.Any(n => n.Contains(".safe")))
        {
            AddPackerNode(peNode, "Safengine", "Safengine — 代码虚拟化与授权保护系统", sectionTableOffset);
            return;
        }

        // ——— StarForce
        if (lower.Any(n => n.StartsWith(".!sf") || n.StartsWith(".sf_")))
        {
            AddPackerNode(peNode, "StarForce", "StarForce — 俄罗斯防篡改与授权保护方案", sectionTableOffset);
            return;
        }

        // ——— ASProtect
        if (lower.Any(n => n.Contains("aspr") || n.Contains("asprotect")))
        {
            AddPackerNode(peNode, "ASProtect", "ASProtect — ASPack 作者的软件授权与保护方案", sectionTableOffset);
            return;
        }

        // ——— Morphine
        if (lower.Any(n => n.Contains(".morph") || n is ".crypted"))
        {
            AddPackerNode(peNode, "Morphine", "Morphine — 加密壳，以不增加文件大小为特点", sectionTableOffset);
            return;
        }

        // ——— VProtect
        if (lower.Any(n => n.Contains(".vprot") || n.Contains("vprot")))
        {
            AddPackerNode(peNode, "VProtect", "VProtect — 可执行文件虚拟机保护壳", sectionTableOffset);
            return;
        }

        // ——— ACProtect
        if (lower.Any(n => n.Contains(".acp") || n.Contains("acprotect")))
        {
            AddPackerNode(peNode, "ACProtect", "ACProtect — 可执行文件保护壳，支持反调试与反内存转储", sectionTableOffset);
            return;
        }

        // ——— PESpin
        if (lower.Any(n => n.Contains(".spin") || n.Contains(".pes")))
        {
            AddPackerNode(peNode, "PESpin", "PESpin — 可执行文件加壳保护工具，支持多种反调试技术", sectionTableOffset);
            return;
        }

        // ——— 启发式检测：如果入口点指向节区 RawSize=0 但 VirtualSize>0 的区域，可能加壳
        // 这类特征常见于未知/自研壳
        if (sectionInfos.Count > 0 && entryPoint > 0)
        {
            var epSectionIdx = -1;
            for (int i = 0; i < sectionInfos.Count; i++)
            {
                var (_, va, _, _, _) = sectionInfos[i];
                var nextVa = i + 1 < sectionInfos.Count ? sectionInfos[i + 1].virtualAddress : uint.MaxValue;
                if (entryPoint >= va && entryPoint < nextVa)
                {
                    epSectionIdx = i;
                    break;
                }
            }

            if (epSectionIdx >= 0)
            {
                var (vs, _, rs, ro, _) = sectionInfos[epSectionIdx];
                // 入口点所在节 RawSize 远小于 VirtualSize，可能被压缩
                if (rs > 0 && vs > rs * 3 && vs > 4096)
                {
                    AddPackerNode(peNode, "⚠ 可能加壳（启发式）",
                        $"入口点 ({entryPoint:X8}) 所在节区 RawSize ({rs}) << VirtualSize ({vs})，可能被压缩或加壳",
                        sectionTableOffset);
                }
            }
        }
    }

    /// <summary>在 PE 结构树下添加加壳检测结果节点</summary>
    private static void AddPackerNode(StructureNode peNode, string packerName, string description, long offset)
    {
        var sectionTableNode = peNode.Children.FirstOrDefault(c => c.Name != null && c.Name.Contains("Section Table"));
        var length = sectionTableNode?.Length ?? 0;

        peNode.AddChild(new StructureNode
        {
            Name = $"🔒 {packerName}",
            Offset = offset,
            Length = length,
            DataType = FieldDataType.Bytes,
            Confidence = 0.9,
            Source = StructureNodeSource.AutoDetected,
            Description = description,
        });
    }

    /// <summary>
    /// APK 后处理：扫描 ZIP 中央目录识别 APK 组件，检测 APK 签名块
    /// </summary>
    private static void PostProcessApk(BinaryBuffer buffer, StructureNode apkNode)
    {
        try
        {
            // 1. 定位 EOCD（从文件尾扫描 PK\x05\x06）
            var eocdOffset = FindEocdOffset(buffer);
            if (eocdOffset < 0) return;

            var totalEntries = buffer.ReadUInt16(eocdOffset + 8, true);
            var cdOffset = buffer.ReadUInt32(eocdOffset + 16, true);
            if (totalEntries == 0 || cdOffset >= buffer.Length) return;

            // 2. 扫描 Central Directory，收集 APK 关键组件
            var components = new List<(string name, long offset, long length, bool isEncrypted)>();
            long cdPos = cdOffset;
            int count = 0;

            while (count < totalEntries && count < 10000 && cdPos + 46 <= buffer.Length)
            {
                var sig = buffer.ReadUInt32(cdPos, true);
                if (sig != 0x02014B50) break; // 不是有效的 Central Directory 条目

                var flags = buffer.ReadUInt16(cdPos + 8, true);
                var compSize = buffer.ReadUInt32(cdPos + 20, true);
                var uncompSize = buffer.ReadUInt32(cdPos + 24, true);
                var nameLen = buffer.ReadUInt16(cdPos + 28, true);
                var extraLen = buffer.ReadUInt16(cdPos + 30, true);
                var commentLen = buffer.ReadUInt16(cdPos + 32, true);
                var localHeaderOff = buffer.ReadUInt32(cdPos + 42, true);

                string fileName;
                if (nameLen > 0 && cdPos + 46 + nameLen <= buffer.Length)
                {
                    var nameBytes = buffer.ReadBytes(cdPos + 46, nameLen);
                    fileName = Encoding.UTF8.GetString(nameBytes);
                }
                else
                {
                    fileName = $"entry_{count}";
                }

                // 只收集 APK 关键组件
                var lowerName = fileName.ToLowerInvariant();
                bool isComponent = lowerName == "androidmanifest.xml"
                    || lowerName == "resources.arsc"
                    || lowerName.EndsWith(".dex")
                    || lowerName.StartsWith("lib/")
                    || lowerName.StartsWith("meta-inf/")
                    || lowerName.StartsWith("res/");

                if (isComponent)
                {
                    long dataOffset = 0;
                    if (localHeaderOff + 30 <= buffer.Length)
                    {
                        var fnLen = buffer.ReadUInt16(localHeaderOff + 26, true);
                        var localExtraLen = buffer.ReadUInt16(localHeaderOff + 28, true);
                        dataOffset = localHeaderOff + 30 + fnLen + localExtraLen;
                    }
                    var dataLen = compSize > 0 ? (long)compSize : (long)uncompSize;
                    components.Add((fileName, dataOffset, dataLen, (flags & 1) != 0));
                }

                cdPos += 46 + nameLen + extraLen + commentLen;
                count++;
            }

            // 3. 添加 APK 组件汇总节点
            if (components.Count > 0)
            {
                var compsNode = new StructureNode
                {
                    Name = $"APK Components ({components.Count})",
                    Offset = 0,
                    Length = buffer.Length,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.85,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "APK 关键组件（DEX、Manifest、原生库等）",
                };
                foreach (var (name, off, len, enc) in components)
                {
                    var cn = new StructureNode
                    {
                        Name = name,
                        Offset = off,
                        Length = len,
                        DataType = FieldDataType.Bytes,
                        Confidence = 0.9,
                        Source = StructureNodeSource.AutoDetected,
                        Description = enc ? $"[加密] APK 组件: {name}" : $"APK 组件: {name}",
                    };
                    if (name.Contains("classes") && name.EndsWith(".dex"))
                        cn.Description = $"Dalvik 可执行文件: {name}";
                    else if (name.Equals("AndroidManifest.xml", StringComparison.OrdinalIgnoreCase))
                        cn.Description = "Android 应用清单（二进制 AXML 格式）";
                    else if (name.Equals("resources.arsc", StringComparison.OrdinalIgnoreCase))
                        cn.Description = "已编译的资源表";
                    else if (name.StartsWith("lib/"))
                        cn.Description = $"原生库 (.so): {name}";
                    else if (name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                        cn.Description = $"APK 签名/元数据: {name}";
                    compsNode.AddChild(cn);
                }
                apkNode.AddChild(compsNode);
            }

            // 4. 检测 APK Signature Scheme v2/v3 签名块
            DetectApkSigningBlock(buffer, apkNode, cdOffset);
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>从文件尾扫描查找 EOCD 签名 (0x06054B50)</summary>
    private static long FindEocdOffset(BinaryBuffer buffer)
    {
        var tailSize = (int)Math.Min(buffer.Length, 0x100FF); // 64KB + 255 字节注释
        var tail = buffer.ReadBytes(buffer.Length - tailSize, tailSize);
        for (int i = tail.Length - 22; i >= 0; i--)
        {
            if (tail[i] == 0x50 && tail[i + 1] == 0x4B &&
                tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                return buffer.Length - tailSize + i;
        }
        return -1;
    }

    /// <summary>检测 APK Signature Scheme v2/v3 签名块</summary>
    private static void DetectApkSigningBlock(BinaryBuffer buffer, StructureNode apkNode, long cdOffset)
    {
        // APK 签名块魔数: "APK Sig Block 42" (16 字节)
        byte[] magic = [0x41, 0x50, 0x4B, 0x53, 0x69, 0x67, 0x20,
                        0x42, 0x6C, 0x6F, 0x63, 0x6B, 0x20, 0x34, 0x32];

        // 从 Central Directory 向前搜索魔数（最多 1MB）
        var searchStart = Math.Max(0, cdOffset - 1024 * 1024);
        long magicOffset = -1;
        for (long i = cdOffset - magic.Length; i >= searchStart && magicOffset < 0; i--)
        {
            bool found = true;
            for (int j = 0; j < magic.Length; j++)
                if (buffer.ReadByte(i + j) != magic[j]) { found = false; break; }
            if (found) magicOffset = i;
        }
        if (magicOffset < 0) return;

        // 魔数前的 8 字节 = block size（重复两次）
        if (magicOffset - 16 < 0) return;
        var blockSize = buffer.ReadUInt64(magicOffset - 8, true);
        var blockSizeCheck = buffer.ReadUInt64(magicOffset - 16, true);
        if (blockSize != blockSizeCheck || blockSize == 0 || blockSize > 10 * 1024 * 1024) return;

        var blockStart = magicOffset - 16 - (long)blockSize;
        if (blockStart < 0) return;

        var sigBlockNode = new StructureNode
        {
            Name = "APK Signing Block",
            Offset = blockStart,
            Length = (long)(blockSize + 24),
            DataType = FieldDataType.Struct,
            Confidence = 0.9,
            Source = StructureNodeSource.AutoDetected,
            Description = "APK Signature Scheme v2/v3 签名块区域（含 ID-value 对）",
        };

        // 扫描 ID-value 对
        long pos = blockStart + 8; // 跳过第一个 blockSize 字段
        long end = magicOffset - 8; // 到第二个 blockSize 之前
        while (pos + 12 <= end)
        {
            var blockId = buffer.ReadUInt32(pos, false); // BigEndian
            var valueLen = buffer.ReadUInt64(pos + 4, true);
            if (valueLen == 0 || valueLen > 10 * 1024 * 1024) break;
            if (pos + 12 + (long)valueLen > end) break;

            string? sigName = blockId switch
            {
                0x7109871a => "APK Signature Scheme v2",
                0xf05368c0 => "APK Signature Scheme v3",
                0x6dff8017 => "APK Signature Scheme v3.1",
                0x0d0670fd => "APK Source Stamp",
                _ => null,
            };

            if (sigName != null)
            {
                sigBlockNode.AddChild(new StructureNode
                {
                    Name = sigName,
                    Offset = pos,
                    Length = (long)(valueLen + 12),
                    DataType = FieldDataType.Struct,
                    Confidence = 0.9,
                    Source = StructureNodeSource.AutoDetected,
                    Description = $"Block ID=0x{blockId:X8}, 数据长度={valueLen}",
                });
            }

            pos += 12 + (long)valueLen;
        }

        if (sigBlockNode.Children.Count > 0)
            apkNode.AddChild(sigBlockNode);
    }

    /// <summary>
    /// EPUB 后处理：扫描 ZIP 中央目录识别 EPUB 关键组件
    /// </summary>
    private static void PostProcessEpub(BinaryBuffer buffer, StructureNode epubNode)
    {
        try
        {
            var eocdOffset = FindEocdOffset(buffer);
            if (eocdOffset < 0) return;

            var totalEntries = buffer.ReadUInt16(eocdOffset + 8, true);
            var cdOffset = buffer.ReadUInt32(eocdOffset + 16, true);
            if (totalEntries == 0 || cdOffset >= buffer.Length) return;

            var components = new List<(string name, long offset, long length, string description)>();
            long cdPos = cdOffset;
            int count = 0;

            while (count < totalEntries && count < 10000 && cdPos + 46 <= buffer.Length)
            {
                var sig = buffer.ReadUInt32(cdPos, true);
                if (sig != 0x02014B50) break;

                var compSize = buffer.ReadUInt32(cdPos + 20, true);
                var uncompSize = buffer.ReadUInt32(cdPos + 24, true);
                var nameLen = buffer.ReadUInt16(cdPos + 28, true);
                var extraLen = buffer.ReadUInt16(cdPos + 30, true);
                var commentLen = buffer.ReadUInt16(cdPos + 32, true);
                var localHeaderOff = buffer.ReadUInt32(cdPos + 42, true);

                string fileName;
                if (nameLen > 0 && cdPos + 46 + nameLen <= buffer.Length)
                {
                    var nameBytes = buffer.ReadBytes(cdPos + 46, nameLen);
                    fileName = Encoding.UTF8.GetString(nameBytes);
                }
                else
                {
                    fileName = $"entry_{count}";
                }

                var lowerName = fileName.ToLowerInvariant();
                bool isComponent = lowerName == "mimetype"
                    || lowerName == "meta-inf/container.xml"
                    || lowerName.EndsWith(".opf")
                    || lowerName.EndsWith(".ncx");

                if (isComponent)
                {
                    long dataOffset = 0;
                    if (localHeaderOff + 30 <= buffer.Length)
                    {
                        var fnLen = buffer.ReadUInt16(localHeaderOff + 26, true);
                        var localExtraLen = buffer.ReadUInt16(localHeaderOff + 28, true);
                        dataOffset = localHeaderOff + 30 + fnLen + localExtraLen;
                    }
                    var dataLen = compSize > 0 ? (long)compSize : (long)uncompSize;

                    string desc = fileName switch
                    {
                        "mimetype" => "EPUB 标识文件 (application/epub+zip)",
                        "META-INF/container.xml" => "EPUB 容器元数据（指向内容清单）",
                        _ when fileName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase)
                            => "EPUB 内容包清单 (OPF)",
                        _ when fileName.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase)
                            => "EPUB 导航控制文件 (NCX)",
                        _ => $"EPUB 组件: {fileName}",
                    };
                    components.Add((fileName, dataOffset, dataLen, desc));
                }

                cdPos += 46 + nameLen + extraLen + commentLen;
                count++;
            }

            if (components.Count > 0)
            {
                var compsNode = new StructureNode
                {
                    Name = $"EPUB Components ({components.Count})",
                    Offset = 0, Length = buffer.Length,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.85,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "EPUB 关键组件（容器元数据、内容清单等）",
                };
                foreach (var (name, off, len, desc) in components)
                {
                    compsNode.AddChild(new StructureNode
                    {
                        Name = name,
                        Offset = off, Length = len,
                        DataType = FieldDataType.Bytes,
                        Confidence = 0.9,
                        Source = StructureNodeSource.AutoDetected,
                        Description = desc,
                    });
                }
                epubNode.AddChild(compsNode);
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// MOBI 后处理：解析 PalmDB 头、记录索引表、MOBI 头及 EXTH 扩展头
    /// </summary>
    private static void PostProcessMobi(BinaryBuffer buffer, StructureNode mobiNode)
    {
        try
        {
            if (buffer.Length < 128) return;

            // 1. PalmDB Header (78 字节)
            var palmHeader = new StructureNode
            {
                Name = "PalmDB Header",
                Offset = 0, Length = 78,
                DataType = FieldDataType.Struct,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
                Description = "Palm Database 文件头 (78 bytes)",
            };
            var nameBytes = buffer.ReadBytes(0, Math.Min(32, (int)buffer.Length));
            var palmName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
            palmHeader.AddChild(MakeField("Name", palmName, 0, 32, FieldDataType.ASCII, 0.95));
            palmHeader.AddChild(MakeField("Attributes", $"0x{buffer.ReadUInt16(32, true):X4}", 32, 2, FieldDataType.UInt16LE, 0.9));
            palmHeader.AddChild(MakeField("Version", $"{buffer.ReadUInt16(34, true)}", 34, 2, FieldDataType.UInt16LE, 0.9));
            palmHeader.AddChild(MakeField("CreationDate", $"{buffer.ReadUInt32(36, true)} (Unix)", 36, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("ModificationDate", $"{buffer.ReadUInt32(40, true)}", 40, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("LastBackupDate", $"{buffer.ReadUInt32(44, true)}", 44, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("ModificationNumber", $"{buffer.ReadUInt32(48, true)}", 48, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("AppInfoID", $"0x{buffer.ReadUInt32(52, true):X8}", 52, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("SortInfoID", $"0x{buffer.ReadUInt32(56, true):X8}", 56, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("Type", "BOOK", 60, 4, FieldDataType.ASCII, 0.95));
            palmHeader.AddChild(MakeField("Creator", "MOBI", 64, 4, FieldDataType.ASCII, 0.95));
            palmHeader.AddChild(MakeField("UniqueIDSeed", $"0x{buffer.ReadUInt32(68, true):X8}", 68, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("NextRecordListID", $"0x{buffer.ReadUInt32(72, true):X8}", 72, 4, FieldDataType.UInt32LE, 0.9));
            palmHeader.AddChild(MakeField("NumRecords", $"{buffer.ReadUInt16(76, true)}", 76, 2, FieldDataType.UInt16LE, 0.9));
            mobiNode.AddChild(palmHeader);

            var numRecords = buffer.ReadUInt16(76, true);

            // 2. Record Info List (8 字节/条，自偏移 78 起)
            if (numRecords > 0 && numRecords < 10000)
            {
                var recordsNode = new StructureNode
                {
                    Name = $"Record Info List ({numRecords} entries)",
                    Offset = 78, Length = numRecords * 8L,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.9,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "PalmDB 记录索引表",
                };

                for (int i = 0; i < numRecords && i < 100; i++)
                {
                    var recOff = 78 + i * 8;
                    if (recOff + 8 > buffer.Length) break;

                    var dataOff = buffer.ReadUInt32(recOff, true);
                    var attr = buffer.ReadByte(recOff + 4);
                    var uniqueIdLow = buffer.ReadUInt16(recOff + 5, false); // BigEndian
                    var uniqueIdHigh = buffer.ReadByte(recOff + 7);

                    recOff = i * 8; // Reset for display offset
                    // Wait, let me recalculate display offset
                }
                // Re-do: display calculation
                recordsNode.Children.Clear();
                for (int i = 0; i < numRecords && i < 100; i++)
                {
                    var entryOffset = 78 + i * 8;
                    if (entryOffset + 8 > buffer.Length) break;

                    var dataOff = buffer.ReadUInt32(entryOffset, true);
                    var attr = buffer.ReadByte(entryOffset + 4);
                    var uniqueId = (buffer.ReadUInt16(entryOffset + 5, false) << 8) | buffer.ReadByte(entryOffset + 7);

                    var isFirst = i == 0;
                    recordsNode.AddChild(new StructureNode
                    {
                        Name = isFirst ? "Record[0] (MOBI Header)" : $"Record[{i}]",
                        Offset = entryOffset, Length = 8,
                        DataType = FieldDataType.Struct,
                        Confidence = 0.9,
                        Source = StructureNodeSource.AutoDetected,
                        Description = isFirst
                            ? "包含 MOBI 头部信息的首个记录"
                            : $"数据偏移=0x{dataOff:X}, 属性=0x{attr:X2}, UniqueID=0x{uniqueId:X}",
                    });
                }
                if (recordsNode.Children.Count > 0)
                    mobiNode.AddChild(recordsNode);
            }

            // 3. MOBI Header（在首个记录的数据中，从记录列表末尾开始搜索 "MOBI"）
            var recordListEnd = 78 + numRecords * 8L;
            long mobiHeaderOffset = -1;
            for (long i = recordListEnd; i < buffer.Length - 4 && mobiHeaderOffset < 0; i++)
            {
                if (buffer.ReadByte(i) == 0x4D && buffer.ReadByte(i + 1) == 0x4F &&
                    buffer.ReadByte(i + 2) == 0x42 && buffer.ReadByte(i + 3) == 0x49)
                    mobiHeaderOffset = i;
            }

            if (mobiHeaderOffset >= 0 && mobiHeaderOffset + 232 <= buffer.Length)
            {
                var mobiHdr = new StructureNode
                {
                    Name = "MOBI Header",
                    Offset = mobiHeaderOffset, Length = 232,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.9,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "MOBI 格式头部（232 字节标准头）",
                };
                mobiHdr.AddChild(MakeField("Identifier", "MOBI", mobiHeaderOffset, 4, FieldDataType.ASCII, 0.95));
                mobiHdr.AddChild(MakeField("HeaderLength", $"{buffer.ReadUInt32(mobiHeaderOffset + 4, false)} bytes", mobiHeaderOffset + 4, 4, FieldDataType.UInt32BE, 0.9));
                var mobiType = buffer.ReadUInt32(mobiHeaderOffset + 8, false);
                string mobiTypeName = mobiType switch
                {
                    2 => "Mobipocket Book",
                    3 => "PalmDoc",
                    257 => "News",
                    258 => "News Feed",
                    259 => "News Magazine",
                    513 => "PICS", 514 => "WORD", 515 => "XLS",
                    516 => "PPT", 517 => "TEXT", 518 => "HTML",
                    _ => $"Unknown ({mobiType})",
                };
                mobiHdr.AddChild(MakeField("MobiType", mobiTypeName, mobiHeaderOffset + 8, 4, FieldDataType.UInt32BE, 0.9));
                var textEncoding = buffer.ReadUInt32(mobiHeaderOffset + 12, false);
                mobiHdr.AddChild(MakeField("TextEncoding", $"CodePage {textEncoding}", mobiHeaderOffset + 12, 4, FieldDataType.UInt32BE, 0.9));
                mobiHdr.AddChild(MakeField("UniqueID", $"{buffer.ReadUInt32(mobiHeaderOffset + 16, false)}", mobiHeaderOffset + 16, 4, FieldDataType.UInt32BE, 0.9));
                mobiHdr.AddChild(MakeField("FileVersion", $"{buffer.ReadUInt32(mobiHeaderOffset + 20, false)}", mobiHeaderOffset + 20, 4, FieldDataType.UInt32BE, 0.9));

                // 尝试读取标题偏移
                var titleOff = buffer.ReadUInt32(mobiHeaderOffset + 84, false);
                var fullNameOff = buffer.ReadUInt32(mobiHeaderOffset + 88, false);
                if (fullNameOff > 0 && mobiHeaderOffset + fullNameOff + 2 <= buffer.Length)
                {
                    // 读取直到空终止符
                    var maxTitleLen = Math.Min(256, (int)(buffer.Length - mobiHeaderOffset - fullNameOff));
                    var titleBytes = buffer.ReadBytes(mobiHeaderOffset + fullNameOff, maxTitleLen);
                    var titleEnd = Array.IndexOf<byte>(titleBytes, 0);
                    if (titleEnd > 0)
                    {
                        var title = Encoding.UTF8.GetString(titleBytes, 0, titleEnd);
                        mobiHdr.AddChild(MakeField("FullName", title, mobiHeaderOffset + fullNameOff, titleEnd, FieldDataType.UTF8, 0.85));
                    }
                }

                mobiNode.AddChild(mobiHdr);

                // 4. EXTH Extended Header（MOBI 头长度 > 232 时存在）
                if (mobiHeaderOffset + 232 + 4 <= buffer.Length)
                {
                    var exthSig = buffer.ReadBytes(mobiHeaderOffset + 232, 4);
                    if (exthSig[0] == 0x45 && exthSig[1] == 0x58 &&
                        exthSig[2] == 0x54 && exthSig[3] == 0x48)
                    {
                        var exthLen = buffer.ReadUInt32(mobiHeaderOffset + 236, false);
                        if (exthLen >= 12 && exthLen <= 65536 && mobiHeaderOffset + 232 + exthLen <= buffer.Length)
                        {
                            var exthNode = new StructureNode
                            {
                                Name = "EXTH Header (Extended)",
                                Offset = mobiHeaderOffset + 232, Length = exthLen,
                                DataType = FieldDataType.Struct,
                                Confidence = 0.85,
                                Source = StructureNodeSource.AutoDetected,
                                Description = "MOBI 扩展头部（包含作者、书名等元数据）",
                            };
                            exthNode.AddChild(MakeField("Identifier", "EXTH", mobiHeaderOffset + 232, 4, FieldDataType.ASCII, 0.95));
                            exthNode.AddChild(MakeField("HeaderLength", $"{exthLen}", mobiHeaderOffset + 236, 4, FieldDataType.UInt32BE, 0.9));
                            var exthRecordCount = buffer.ReadUInt32(mobiHeaderOffset + 240, false);
                            exthNode.AddChild(MakeField("RecordCount", $"{exthRecordCount}", mobiHeaderOffset + 240, 4, FieldDataType.UInt32BE, 0.9));

                            long exthPos = mobiHeaderOffset + 244;
                            for (int i = 0; i < exthRecordCount && exthPos + 8 <= mobiHeaderOffset + 232 + exthLen; i++)
                            {
                                var recType = buffer.ReadUInt32(exthPos, false);
                                var recLen = buffer.ReadUInt32(exthPos + 4, false);
                                if (recLen < 8 || exthPos + recLen > mobiHeaderOffset + 232 + exthLen) break;

                                var recDataLen = recLen - 8;
                                string? value = null;
                                if (recDataLen > 0 && recDataLen <= 256 && exthPos + 8 + recDataLen <= buffer.Length)
                                {
                                    var valBytes = buffer.ReadBytes(exthPos + 8, (int)recDataLen);
                                    value = Encoding.UTF8.GetString(valBytes).TrimEnd('\0');
                                }

                                string recName = recType switch
                                {
                                    100 => "Author", 101 => "Publisher", 102 => "Imprint",
                                    103 => "Description", 104 => "ISBN", 105 => "Subject",
                                    106 => "PublishingDate", 108 => "Review", 109 => "Contributor",
                                    110 => "Rights", 111 => "SubjectCode", 112 => "Type",
                                    113 => "Source", 114 => "ASIN", 115 => "VersionNumber",
                                    116 => "Sample", 117 => "StartReading", 118 => "RetailPrice",
                                    119 => "RetailCurrency",
                                    _ => $"Record[{recType}]",
                                };

                                exthNode.AddChild(new StructureNode
                                {
                                    Name = recName,
                                    Offset = exthPos, Length = recLen,
                                    DataType = FieldDataType.ASCII,
                                    Confidence = 0.85,
                                    Source = StructureNodeSource.AutoDetected,
                                    Description = value ?? $"(binary, {recDataLen} bytes)",
                                });
                                exthPos += recLen;
                            }
                            mobiNode.AddChild(exthNode);
                        }
                    }
                }
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// CRX 后处理：计算 ZIP 数据偏移，扫描中央目录识别扩展关键文件
    /// </summary>
    private static void PostProcessCrx(BinaryBuffer buffer, StructureNode crxNode)
    {
        try
        {
            if (buffer.Length < 16) return;

            // 1. 确定 CRX 版本和 ZIP 数据偏移
            var version = buffer.ReadUInt32(4, true);
            long zipDataOffset;

            if (version == 2)
            {
                if (buffer.Length < 16) return;
                var pubKeyLen = buffer.ReadUInt32(8, true);
                var sigLen = buffer.ReadUInt32(12, true);
                zipDataOffset = 16L + pubKeyLen + sigLen;
            }
            else if (version == 3)
            {
                if (buffer.Length < 12) return;
                var headerLen = buffer.ReadUInt32(8, true);
                zipDataOffset = 12L + headerLen;
            }
            else
            {
                return; // 未知版本
            }

            if (zipDataOffset >= buffer.Length) return;

            // 添加 ZIP 数据偏移信息节点
            crxNode.AddChild(new StructureNode
            {
                Name = $"ZIP Data @ offset 0x{zipDataOffset:X}",
                Offset = zipDataOffset,
                Length = buffer.Length - zipDataOffset,
                DataType = FieldDataType.Bytes,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
                Description = $"CRX v{version} ZIP 数据区域（自偏移 0x{zipDataOffset:X} 起）",
            });

            // 2. 在 ZIP 数据范围内扫描 EOCD（用全局扫描然后验证偏移）
            var eocdOffset = FindEocdOffset(buffer);
            if (eocdOffset < 0) return;

            var totalEntries = buffer.ReadUInt16(eocdOffset + 8, true);
            var cdOffset = buffer.ReadUInt32(eocdOffset + 16, true);
            var actualCdOffset = zipDataOffset + cdOffset;
            if (totalEntries == 0 || actualCdOffset >= buffer.Length) return;

            // 3. 扫描 Central Directory 收集 CRX 关键文件
            var components = new List<(string name, long offset, long length, string description)>();
            long cdPos = actualCdOffset;
            int count = 0;

            while (count < totalEntries && count < 10000 && cdPos + 46 <= buffer.Length)
            {
                var sig = buffer.ReadUInt32(cdPos, true);
                if (sig != 0x02014B50) break;

                var compSize = buffer.ReadUInt32(cdPos + 20, true);
                var uncompSize = buffer.ReadUInt32(cdPos + 24, true);
                var nameLen = buffer.ReadUInt16(cdPos + 28, true);
                var extraLen = buffer.ReadUInt16(cdPos + 30, true);
                var commentLen = buffer.ReadUInt16(cdPos + 32, true);
                var localHeaderOff = buffer.ReadUInt32(cdPos + 42, true);

                string fileName;
                if (nameLen > 0 && cdPos + 46 + nameLen <= buffer.Length)
                {
                    var nameBytes = buffer.ReadBytes(cdPos + 46, nameLen);
                    fileName = Encoding.UTF8.GetString(nameBytes);
                }
                else
                {
                    fileName = $"entry_{count}";
                }

                // 只收集 CRX 关键文件
                var lowerName = fileName.ToLowerInvariant();
                bool isComponent = lowerName == "manifest.json"
                    || lowerName.StartsWith("_locales/")
                    || lowerName.StartsWith("icons/")
                    || lowerName.StartsWith("js/")
                    || lowerName.StartsWith("css/")
                    || lowerName.StartsWith("_metadata/");

                if (isComponent)
                {
                    long dataOffset = 0;
                    var actualLocalHeaderOff = zipDataOffset + localHeaderOff;
                    if (actualLocalHeaderOff + 30 <= buffer.Length)
                    {
                        var fnLen = buffer.ReadUInt16(actualLocalHeaderOff + 26, true);
                        var localExtraLen = buffer.ReadUInt16(actualLocalHeaderOff + 28, true);
                        dataOffset = actualLocalHeaderOff + 30 + fnLen + localExtraLen;
                    }
                    var dataLen = compSize > 0 ? (long)compSize : (long)uncompSize;

                    string desc = fileName switch
                    {
                        "manifest.json" => "Chrome 扩展清单文件（名称、版本、权限等）",
                        _ when lowerName.StartsWith("_locales/") => $"国际化资源: {fileName}",
                        _ when lowerName.StartsWith("icons/") => $"扩展图标: {fileName}",
                        _ when lowerName.StartsWith("_metadata/") => $"Chrome 商店元数据: {fileName}",
                        _ => $"扩展文件: {fileName}",
                    };
                    components.Add((fileName, dataOffset, dataLen, desc));
                }

                cdPos += 46 + nameLen + extraLen + commentLen;
                count++;
            }

            if (components.Count > 0)
            {
                var compsNode = new StructureNode
                {
                    Name = $"CRX Components ({components.Count})",
                    Offset = zipDataOffset, Length = buffer.Length - zipDataOffset,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.85,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "Chrome 扩展关键组件（清单、图标、脚本、国际化等）",
                };
                foreach (var (name, off, len, desc) in components)
                {
                    compsNode.AddChild(new StructureNode
                    {
                        Name = name,
                        Offset = off, Length = len,
                        DataType = FieldDataType.Bytes,
                        Confidence = 0.9,
                        Source = StructureNodeSource.AutoDetected,
                        Description = desc,
                    });
                }
                crxNode.AddChild(compsNode);
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// LNK 后处理：解析 LinkTargetIDList、LocationInfo、Data Strings、Extra Data
    /// </summary>
    private static void PostProcessLnk(BinaryBuffer buffer, StructureNode lnkNode)
    {
        try
        {
            if (buffer.Length < 76) return;
            var linkFlags = buffer.ReadUInt32(20, true);

            // ——— LinkFlags 解码 ———
            var flagsNode = new StructureNode
            {
                Name = "LinkFlags",
                Offset = 20, Length = 4,
                DataType = FieldDataType.Struct,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
                Description = "Shell Link Header 标志位",
            };
            string[] flagNames = [
                "HasLinkTargetIDList", "HasLinkInfo", "HasName", "HasRelativePath",
                "HasWorkingDir", "HasArguments", "HasIconLocation", "IsUnicode",
                "ForceNoLinkInfo", "HasExpString", "RunInSeparateProcess", "",
                "HasDarwinID", "RunAsUser", "HasExpIcon", "NoPidlAlias",
                "", "RunWithShimLayer", "ForceNoLinkTrack", "EnableTargetMetadata",
                "DisableLinkPathTracking", "DisableKnownFolderTracking", "DisableKnownFolderAlias",
                "AllowLinkToLink", "UnaliasOnSave", "PreferEnvironmentPath", "KeepLocalIDListForUNCTarget",
            ];
            for (int i = 0; i < 28 && i < flagNames.Length; i++)
            {
                if (string.IsNullOrEmpty(flagNames[i])) continue;
                var set = (linkFlags & (1u << i)) != 0;
                flagsNode.AddChild(MakeField(flagNames[i], set ? "✓ 已设置" : "", 20, 4, FieldDataType.Bytes, 0.85));
            }
            lnkNode.AddChild(flagsNode);

            long pos = 76; // 当前解析位置

            // ——— LinkTargetIDList ———
            if ((linkFlags & 0x01) != 0 && pos + 2 <= buffer.Length) // HasLinkTargetIDList
            {
                var idListSize = buffer.ReadUInt16(pos, true);
                if (idListSize >= 2 && pos + 2 + idListSize <= buffer.Length)
                {
                    var idListNode = new StructureNode
                    {
                        Name = "LinkTargetIDList",
                        Offset = pos, Length = 2 + idListSize,
                        DataType = FieldDataType.Struct,
                        Confidence = 0.85,
                        Source = StructureNodeSource.AutoDetected,
                        Description = "Shell 命名空间目标路径",
                    };
                    idListNode.AddChild(MakeField("IDListSize", $"{idListSize} bytes", pos, 2, FieldDataType.UInt16LE, 0.9));
                    idListNode.AddChild(MakeField("ItemID序列", $"{(idListSize - 2) / 2} 个 ItemID", pos + 2, idListSize - 2, FieldDataType.Bytes, 0.85));
                    lnkNode.AddChild(idListNode);
                    pos += 2 + idListSize;
                }
                else pos += 2;
            }

            // ——— LocationInfo ———
            if ((linkFlags & 0x02) != 0 && pos + 4 <= buffer.Length) // HasLinkInfo
            {
                var locInfoSize = buffer.ReadUInt32(pos, true);
                if (locInfoSize >= 12 && pos + locInfoSize <= buffer.Length)
                {
                    var locInfoNode = new StructureNode
                    {
                        Name = "LocationInfo",
                        Offset = pos, Length = locInfoSize,
                        DataType = FieldDataType.Struct,
                        Confidence = 0.85,
                        Source = StructureNodeSource.AutoDetected,
                        Description = "目标位置信息（本地/网络路径）",
                    };
                    locInfoNode.AddChild(MakeField("LocationInfoSize", $"{locInfoSize}", pos, 4, FieldDataType.UInt32LE, 0.9));
                    var headerSize = buffer.ReadUInt32(pos + 4, true);
                    locInfoNode.AddChild(MakeField("HeaderSize", $"{headerSize}", pos + 4, 4, FieldDataType.UInt32LE, 0.9));
                    var linkInfoFlags = buffer.ReadUInt32(pos + 8, true);
                    locInfoNode.AddChild(MakeField("LinkInfoFlags", $"0x{linkInfoFlags:X8}", pos + 8, 4, FieldDataType.UInt32LE, 0.9));
                    var localBasePathOff = buffer.ReadUInt32(pos + 12, true);
                    var commonNetRelOff = buffer.ReadUInt32(pos + 16, true);
                    var commonSuffixOff = buffer.ReadUInt32(pos + 20, true);

                    // 本地路径（Unicode 字符串）
                    if (localBasePathOff > 0 && localBasePathOff + 8 <= locInfoSize && headerSize >= 0x1C)
                    {
                        var volIdOff = buffer.ReadUInt32(pos + 24, true);
                        if (volIdOff > 0 && pos + volIdOff + 4 <= buffer.Length)
                        {
                            // VolumeID 之后是 LocalBasePath Unicode 字符串
                            var basePathOff = localBasePathOff; // actually localBasePathOffset already
                            var maxLen = Math.Min(520, (int)(buffer.Length - pos - basePathOff));
                            if (basePathOff > 0 && pos + basePathOff < buffer.Length)
                            {
                                var pathBytes = buffer.ReadBytes(pos + basePathOff, Math.Min(maxLen, (int)(locInfoSize - basePathOff)));
                                var nullIdx = Array.IndexOf<byte>(pathBytes, 0);
                                if (nullIdx > 0)
                                {
                                    var localPath = Encoding.Unicode.GetString(pathBytes, 0, nullIdx);
                                    locInfoNode.AddChild(MakeField("LocalBasePath", localPath, pos + basePathOff, nullIdx, FieldDataType.Bytes, 0.85));
                                }
                            }
                        }
                    }

                    // 通用后缀
                    if (commonSuffixOff > 0 && pos + commonSuffixOff + 2 <= buffer.Length)
                    {
                        var maxSufLen = Math.Min(260, (int)(buffer.Length - pos - commonSuffixOff));
                        var sufBytes = buffer.ReadBytes(pos + commonSuffixOff, maxSufLen);
                        var nullSuf = Array.IndexOf<byte>(sufBytes, 0);
                        if (nullSuf > 0)
                        {
                            var suffix = Encoding.Unicode.GetString(sufBytes, 0, nullSuf);
                            locInfoNode.AddChild(MakeField("CommonPathSuffix", suffix, pos + commonSuffixOff, nullSuf, FieldDataType.Bytes, 0.85));
                        }
                    }

                    lnkNode.AddChild(locInfoNode);
                }
                pos += locInfoSize;
            }

            // ——— Data Strings (Unicode) ———
            var stringFlags = new (uint bit, string name)[]
            {
                (0x04, "NameString"),      // HasName
                (0x08, "RelativePath"),    // HasRelativePath
                (0x10, "WorkingDir"),      // HasWorkingDir
                (0x20, "Arguments"),       // HasArguments
                (0x40, "IconLocation"),    // HasIconLocation
            };

            foreach (var (bit, name) in stringFlags)
            {
                if ((linkFlags & bit) == 0) continue;
                if (pos + 2 > buffer.Length) break;

                var charCount = buffer.ReadUInt16(pos, true);
                if ((linkFlags & 0x80) != 0) // IsUnicode
                {
                    var byteLen = charCount * 2;
                    if (charCount == 0 || pos + 2 + byteLen > buffer.Length) { pos += 2; continue; }
                    var strBytes = buffer.ReadBytes(pos + 2, byteLen);
                    var value = Encoding.Unicode.GetString(strBytes).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(value))
                        lnkNode.AddChild(MakeField(name, value, pos, 2 + byteLen, FieldDataType.Bytes, 0.85));
                    pos += 2 + byteLen;
                }
                else
                {
                    if (charCount == 0 || pos + 2 + charCount > buffer.Length) { pos += 2; continue; }
                    var strBytes = buffer.ReadBytes(pos + 2, charCount);
                    var value = Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(value))
                        lnkNode.AddChild(MakeField(name, value, pos, 2 + charCount, FieldDataType.Bytes, 0.85));
                    pos += 2 + charCount;
                }
            }

            // ——— Extra Data Sections ———
            // 从文件尾向前扫描已知 BlockSignature
            if (pos + 8 <= buffer.Length)
            {
                var extraDataNode = new StructureNode
                {
                    Name = "Extra Data",
                    Offset = pos, Length = buffer.Length - pos,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.8,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "附加数据块（环境变量、控制台、跟踪器等）",
                };
                long scanPos = pos;
                while (scanPos + 8 <= buffer.Length)
                {
                    var blockSize = buffer.ReadUInt32(scanPos, true);
                    if (blockSize == 0) break; // 终止符
                    if (blockSize < 8 || scanPos + blockSize > buffer.Length) break;

                    var blockSig = buffer.ReadUInt32(scanPos + 4, true);
                    string sigName = blockSig switch
                    {
                        0xA0000001 => "EnvironmentVariableDataBlock",
                        0xA0000002 => "ConsoleDataBlock",
                        0xA0000003 => "TrackerDataBlock",
                        0xA0000004 => "ConsoleFEDataBlock",
                        0xA0000005 => "SpecialFolderDataBlock",
                        0xA0000006 => "DarwinDataBlock",
                        0xA0000007 => "IconEnvironmentDataBlock",
                        0xA0000008 => "ShimDataBlock",
                        0xA0000009 => "PropertyStoreDataBlock",
                        0xA000000B => "KnownFolderDataBlock",
                        0xA000000C => "VistaAndAboveIDListDataBlock",
                        _ => $"UnknownBlock(0x{blockSig:X8})",
                    };
                    extraDataNode.AddChild(MakeField(sigName, $"Size={blockSize}", scanPos, blockSize, FieldDataType.Bytes, 0.85));
                    scanPos += blockSize;
                }
                if (extraDataNode.Children.Count > 0)
                    lnkNode.AddChild(extraDataNode);
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// DMG 后处理：从文件尾扫描 koly 签名，解析 UDIF 结构
    /// </summary>
    private static void PostProcessDmg(BinaryBuffer buffer, StructureNode dmgNode)
    {
        try
        {
            if (buffer.Length < 512) return;

            // 1. Koly 块位于文件末尾 512 字节
            var kolyOffset = buffer.Length - 512;
            if (kolyOffset < 0) return;
            // 验证 "koly" 签名
            var sig = buffer.ReadUInt32(kolyOffset, true);
            if (sig != 0x796C6F6B) return; // "koly"

            var kolyNode = new StructureNode
            {
                Name = "Koly Footer",
                Offset = kolyOffset, Length = 512,
                DataType = FieldDataType.Struct,
                Confidence = 0.9,
                Source = StructureNodeSource.AutoDetected,
                Description = "UDIF Koly 块 — DMG 文件尾部标识块（512 字节）",
            };

            // 2. 解析 Koly 块字段
            kolyNode.AddChild(MakeField("Signature", $"koly @ 0x{kolyOffset:X}", kolyOffset, 4, FieldDataType.ASCII, 0.95));
            var version = buffer.ReadUInt32(kolyOffset + 4, true);
            kolyNode.AddChild(MakeField("Version", $"{version}", kolyOffset + 4, 4, FieldDataType.UInt32LE, 0.9));
            kolyNode.AddChild(MakeField("HeaderSize", $"{buffer.ReadUInt32(kolyOffset + 8, true)}", kolyOffset + 8, 4, FieldDataType.UInt32LE, 0.9));
            var flags = buffer.ReadUInt32(kolyOffset + 12, true);
            kolyNode.AddChild(MakeField("Flags", $"0x{flags:X8}", kolyOffset + 12, 4, FieldDataType.UInt32LE, 0.9));

            // 数据分叉信息
            var dataForkOffset = buffer.ReadUInt64(kolyOffset + 28, true);
            var dataForkLen = buffer.ReadUInt64(kolyOffset + 36, true);
            kolyNode.AddChild(MakeField("DataForkOffset", $"0x{dataForkOffset:X}", kolyOffset + 28, 8, FieldDataType.UInt64LE, 0.9));
            kolyNode.AddChild(MakeField("DataForkLength", $"{dataForkLen} bytes", kolyOffset + 36, 8, FieldDataType.UInt64LE, 0.9));

            // 资源分叉信息
            kolyNode.AddChild(MakeField("RsrcForkOffset", $"0x{buffer.ReadUInt64(kolyOffset + 44, true):X}", kolyOffset + 44, 8, FieldDataType.UInt64LE, 0.9));
            kolyNode.AddChild(MakeField("RsrcForkLength", $"{buffer.ReadUInt64(kolyOffset + 52, true)} bytes", kolyOffset + 52, 8, FieldDataType.UInt64LE, 0.9));

            // 段信息
            kolyNode.AddChild(MakeField("SegmentNumber", $"{buffer.ReadUInt32(kolyOffset + 60, true)}", kolyOffset + 60, 4, FieldDataType.UInt32LE, 0.9));
            kolyNode.AddChild(MakeField("SegmentCount", $"{buffer.ReadUInt32(kolyOffset + 64, true)}", kolyOffset + 64, 4, FieldDataType.UInt32LE, 0.9));

            // BLKX 分区表位置
            var blkxOffset = buffer.ReadUInt64(kolyOffset + 72, true);
            var blkxCount = buffer.ReadUInt64(kolyOffset + 80, true);
            kolyNode.AddChild(MakeField("BlkxOffset", $"0x{blkxOffset:X}", kolyOffset + 72, 8, FieldDataType.UInt64LE, 0.9));
            kolyNode.AddChild(MakeField("BlkxCount", blkxCount > 0 ? $"分区条目数: {blkxCount}" : "0", kolyOffset + 80, 8, FieldDataType.UInt64LE, 0.9));

            // 校验和
            kolyNode.AddChild(MakeField("ChecksumType", $"{buffer.ReadUInt32(kolyOffset + 88, true)}", kolyOffset + 88, 4, FieldDataType.UInt32LE, 0.9));
            kolyNode.AddChild(MakeField("ChecksumSize", $"{buffer.ReadUInt32(kolyOffset + 92, true)}", kolyOffset + 92, 4, FieldDataType.UInt32LE, 0.9));

            // 尾部签名（重复，用于完整性检查）
            var tailSig = buffer.ReadBytes(kolyOffset + 508, 4);
            var tailSigStr = System.Text.Encoding.ASCII.GetString(tailSig).TrimEnd('\0');
            kolyNode.AddChild(MakeField("TailSignature", tailSigStr, kolyOffset + 508, 4, FieldDataType.ASCII, 0.95));

            dmgNode.AddChild(kolyNode);

            // 3. 解析 BLKX 分区表（XML plist 格式）
            if (blkxOffset > 0 && blkxCount > 0 && (long)blkxOffset + (long)(blkxCount * 200) <= buffer.Length)
            {
                var plistNode = new StructureNode
                {
                    Name = $"Partitions ({blkxCount})",
                    Offset = (long)blkxOffset, Length = 0,
                    DataType = FieldDataType.Struct,
                    Confidence = 0.8,
                    Source = StructureNodeSource.AutoDetected,
                    Description = "DMG 分区表（BLKX XML plist）",
                };

                // 读取 BLKX 数据并尝试解析 XML
                var blkxSize = (int)Math.Min(buffer.Length - (long)blkxOffset, 1024 * 1024);
                var blkxData = buffer.ReadBytes((long)blkxOffset, blkxSize);
                var blkxText = System.Text.Encoding.UTF8.GetString(blkxData);

                // 简单解析：查找 <dict> 块中的关键标记
                var resourceCount = 0;
                var pos = 0;
                while (pos < blkxText.Length && resourceCount < 50)
                {
                    var nameIdx = blkxText.IndexOf("<key>Name</key>", pos, StringComparison.Ordinal);
                    if (nameIdx < 0) break;
                    var strStart = blkxText.IndexOf("<string>", nameIdx, StringComparison.Ordinal);
                    if (strStart < 0) break;
                    strStart += 8;
                    var strEnd = blkxText.IndexOf("</string>", strStart, StringComparison.Ordinal);
                    if (strEnd < 0) break;
                    var name = blkxText[strStart..strEnd];

                    var sizeIdx = blkxText.IndexOf("<key>Size</key>", nameIdx, StringComparison.Ordinal);
                    long partSize = 0;
                    if (sizeIdx >= 0)
                    {
                        var intStart = blkxText.IndexOf("<integer>", sizeIdx, StringComparison.Ordinal);
                        if (intStart >= 0)
                        {
                            intStart += 9;
                            var intEnd = blkxText.IndexOf("</integer>", intStart, StringComparison.Ordinal);
                            if (intEnd >= 0 && long.TryParse(blkxText[intStart..intEnd], out var sz))
                                partSize = sz;
                        }
                    }

                    var partName = string.IsNullOrEmpty(name) ? $"Partition[{resourceCount}]" : name;
                    var partDesc = partSize > 0 ? $"{name} ({FormatFileSizeStatic(partSize)})" : name;
                    plistNode.AddChild(new StructureNode
                    {
                        Name = partName,
                        Offset = (long)blkxOffset + nameIdx,
                        Length = Math.Min(strEnd - nameIdx + 9, 200),
                        DataType = FieldDataType.Bytes,
                        Confidence = 0.8,
                        Source = StructureNodeSource.AutoDetected,
                        Description = partDesc,
                    });
                    resourceCount++;
                    pos = strEnd + 9;
                }

                plistNode.Length = (int)Math.Min(buffer.Length - (long)blkxOffset, plistNode.Children.Count > 0
                    ? plistNode.Children.Max(c => c.Offset + c.Length) - (long)blkxOffset
                    : 0);

                if (plistNode.Children.Count > 0)
                    dmgNode.AddChild(plistNode);
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>格式化文件大小（DMG 后处理用）</summary>
    private static string FormatFileSizeStatic(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    /// <summary>
    /// PYC 后处理：解析 Python 版本、时间戳/哈希、代码对象概要
    /// </summary>
    private static void PostProcessPyc(BinaryBuffer buffer, StructureNode pycNode)
    {
        try
        {
            if (buffer.Length < 12) return;

            // 1. 识别 Python 版本（魔数低 2 字节在高 2 字节 "0D0A" 之前）
            var magic = buffer.ReadUInt32(0, true);
            string pyVersion = magic switch
            {
                0x0D55 => "Python 3.8",
                0x0D61 => "Python 3.9",
                0x0D6F => "Python 3.10",
                0x0DA7 => "Python 3.11",
                0x0C0B => "Python 3.12",
                0x0D0D => "Python 3.13",
                _ => $"Python (magic=0x{magic:X8})",
            };
            pycNode.AddChild(MakeField("Python Version", pyVersion, 0, 4, FieldDataType.Bytes, 0.95));

            // 2. BitField 标志
            var bitField = buffer.ReadUInt32(4, true);
            var hasHash = (bitField & 1) != 0;
            pycNode.AddChild(MakeField("BitField", $"0x{bitField:X8}{(hasHash ? " (hash mode)" : " (timestamp mode)")}", 4, 4, FieldDataType.UInt32LE, 0.9));

            // 3. 时间戳或源哈希
            if (hasHash && buffer.Length >= 16)
            {
                var hashBytes = buffer.ReadBytes(8, 8);
                pycNode.AddChild(MakeField("SourceHash", BitConverter.ToString(hashBytes).Replace("-", ""), 8, 8, FieldDataType.Bytes, 0.9));
            }
            else if (!hasHash && buffer.Length >= 16)
            {
                var timestamp = buffer.ReadUInt32(8, true);
                pycNode.AddChild(MakeField("Timestamp", $"{timestamp}", 8, 4, FieldDataType.UInt32LE, 0.9));
                var srcSize = buffer.ReadUInt32(12, true);
                pycNode.AddChild(MakeField("SourceSize", $"{srcSize} bytes", 12, 4, FieldDataType.UInt32LE, 0.9));
            }

            // 4. Marshalled Code Object 起始
            var codeOffset = 16;
            if (buffer.Length > codeOffset)
            {
                var typeTag = buffer.ReadByte(codeOffset);
                string typeInfo = typeTag switch
                {
                    0x63 => "Code Object (type='c') — 包含字节码、常量表、名称表",
                    0x28 => "Tuple (type='(')",
                    0x5B => "List (type='[')",
                    0x7B => "Dict (type='{')",
                    0x69 => "Integer (type='i')",
                    0x73 => "Short ASCII String (type='s')",
                    0x74 => "Interned String",
                    0x4E => "None",
                    0x54 => "True",
                    0x46 => "False",
                    _ => $"Type 0x{typeTag:X2}",
                };

                if (typeTag == 0x63 && buffer.Length > codeOffset + 9)
                {
                    var argCount = buffer.ReadUInt32(codeOffset + 1, true);
                    var nLocals = buffer.ReadUInt32(codeOffset + 5, true);
                    var stackSize = buffer.ReadUInt32(codeOffset + 9, true);
                    pycNode.AddChild(MakeField("Code Object", $"argcount={argCount}, nlocals={nLocals}, stacksize={stackSize}",
                        codeOffset, 13, FieldDataType.Bytes, 0.85));
                }
                else
                {
                    pycNode.AddChild(MakeField("Marshalled Data", typeInfo,
                        codeOffset, Math.Min(buffer.Length - codeOffset, 30), FieldDataType.Bytes, 0.85));
                }
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// PAK 后处理：解析文件索引尾部，获取文件条目数量和首个文件名
    /// </summary>
    private static void PostProcessPak(BinaryBuffer buffer, StructureNode pakNode)
    {
        try
        {
            if (buffer.Length < 20) return;

            var version = buffer.ReadUInt32(4, true);

            // 扫描尾部查找 "PACK" 或尾部索引偏移
            long trailerOffset = -1;
            for (long i = buffer.Length - 8; i >= buffer.Length - 64 && i >= 0; i--)
            {
                if (buffer.ReadUInt32(i, true) == 0x4B434150) // "PACK" in LE
                {
                    trailerOffset = i;
                    break;
                }
            }
            if (trailerOffset < 0) return;

            // 尾部前 8 字节如果是 PACK 自身，则再往前找 indexOffset
            long indexOffset;
            long indexSize;
            if (version >= 9) // UE5: 两个 8 字节偏移
            {
                if (trailerOffset - 16 < 0) return;
                indexOffset = (long)buffer.ReadUInt64(trailerOffset - 16, true);
                indexSize = (long)buffer.ReadUInt64(trailerOffset - 8, true);
            }
            else // UE4: 一个 8 字节偏移
            {
                if (trailerOffset - 8 < 0) return;
                indexOffset = (long)buffer.ReadUInt64(trailerOffset - 8, true);
                indexSize = 0;
            }

            if (indexOffset < 0 || indexOffset >= buffer.Length) return;

            // 在 indexOffset 处读取 MountPoint（UE4 文件索引起始）
            if (indexOffset + 4 <= buffer.Length)
            {
                // 文件数量（UE4: uint32, UE5: uint64）
                long mountPointEnd = indexOffset;
                // 先读 mountPoint 字符串
                if (indexOffset + 4 <= buffer.Length)
                {
                    var mountLen = buffer.ReadInt32(indexOffset, true);
                    if (mountLen > 0 && mountLen < 1024 && indexOffset + 4 + mountLen <= buffer.Length)
                    {
                        var mpBytes = buffer.ReadBytes(indexOffset + 4, mountLen);
                        var mountPoint = System.Text.Encoding.UTF8.GetString(mpBytes);
                        if (!string.IsNullOrEmpty(mountPoint))
                            pakNode.AddChild(MakeField("MountPoint", mountPoint, indexOffset, 4 + mountLen, FieldDataType.UTF8, 0.9));
                        mountPointEnd = indexOffset + 4 + mountLen;
                    }
                    else
                    {
                        mountPointEnd = indexOffset + 4;
                    }
                }

                // 文件数量
                long fileCount;
                long countOffset = mountPointEnd;
                if (version >= 9)
                {
                    if (countOffset + 8 > buffer.Length) return;
                    fileCount = (long)buffer.ReadUInt64(countOffset, true);
                    pakNode.AddChild(MakeField("FileCount", $"{fileCount}", countOffset, 8, FieldDataType.UInt64LE, 0.9));
                }
                else
                {
                    if (countOffset + 4 > buffer.Length) return;
                    fileCount = buffer.ReadUInt32(countOffset, true);
                    pakNode.AddChild(MakeField("FileCount", $"{fileCount}", countOffset, 4, FieldDataType.UInt32LE, 0.9));
                }

                // 提取第一个条目文件名
                if (fileCount > 0)
                {
                    var entryOffset = countOffset + (version >= 9 ? 8 : 4);
                    if (entryOffset + 4 <= buffer.Length)
                    {
                        var fnLen = buffer.ReadInt32(entryOffset, true);
                        if (fnLen > 0 && fnLen < 512 && entryOffset + 4 + fnLen <= buffer.Length)
                        {
                            var fnBytes = buffer.ReadBytes(entryOffset + 4, fnLen);
                            var firstName = System.Text.Encoding.UTF8.GetString(fnBytes);
                            pakNode.AddChild(MakeField("FirstEntry", firstName, entryOffset, 4 + fnLen, FieldDataType.UTF8, 0.85));
                        }
                    }
                }
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// CAB 后处理：解析文件数量和文件夹数量
    /// </summary>
    private static void PostProcessCab(BinaryBuffer buffer, StructureNode cabNode)
    {
        try
        {
            if (buffer.Length < 36) return;
            var numFiles = buffer.ReadUInt16(24, true);
            var numFolders = buffer.ReadUInt16(22, true);
            cabNode.AddChild(MakeField("NumFiles", $"{numFiles}", 24, 2, FieldDataType.UInt16LE, 0.9));
            cabNode.AddChild(MakeField("NumFolders", $"{numFolders}", 22, 2, FieldDataType.UInt16LE, 0.9));

            // 读取第一个文件名（可选）
            var filesOffset = buffer.ReadUInt32(12, true);
            if (numFiles > 0 && filesOffset > 0 && filesOffset + 8 <= buffer.Length)
            {
                var firstSize = buffer.ReadUInt32(filesOffset, true);
                var nameLenOff = filesOffset + 8; // skip size(4) + offset(4)
                // Find null-terminated filename after folderIndex(2)+date(2)+time(2)+flags(2)
                var nameStart = nameLenOff + 8; // skip folderIndex(2)+date(2)+time(2)+flags(2)
                if (nameStart < buffer.Length)
                {
                    var maxNameLen = (int)Math.Min(buffer.Length - nameStart, 256);
                    var nameBytes = buffer.ReadBytes(nameStart, maxNameLen);
                    var nullIdx = Array.IndexOf<byte>(nameBytes, 0);
                    if (nullIdx > 0)
                    {
                        var firstName = System.Text.Encoding.ASCII.GetString(nameBytes, 0, nullIdx);
                        cabNode.AddChild(MakeField("FirstFile", firstName, nameStart, nullIdx + 1, FieldDataType.ASCII, 0.85));
                    }
                }
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>
    /// 7z 后处理：解析版本、NextHeader 偏移/大小，及 NID 树结构
    /// </summary>
    private static void PostProcess7z(BinaryBuffer buffer, StructureNode szNode)
    {
        try
        {
            if (buffer.Length < 32) return;

            var majorVer = buffer.ReadByte(6);
            var minorVer = buffer.ReadByte(7);

            var nextOff = buffer.ReadUInt64(12, true);
            var nextSize = buffer.ReadUInt64(20, true);
            var nextCrc = buffer.ReadUInt32(28, true);

            // ── 解析 NextHeader（允许 NextHeaderOffset=0，即紧跟在 SignatureHeader 后）───
            if (nextSize > 0 && (long)nextOff >= 0 && 32 + (long)nextOff + (long)nextSize <= buffer.Length)
            {
                var nhStart = 32 + (long)nextOff;
                var nhSize = (long)nextSize;

                // 安全上限：单次最多读取 1MB
                var readSize = Math.Min((int)nhSize, 1024 * 1024);
                var nhData = buffer.ReadBytes(nhStart, readSize);

                var parser = new SevenZipParser.SevenZipHeaderParser();
                var parseResult = parser.Parse(nhData);

                // ── 构建结构树 ──
                var nhNode = MakeField("NextHeader", $"位于 0x{nhStart:X}, size={nhSize}", nhStart, nhSize, FieldDataType.Bytes, 0.85);
                szNode.AddChild(nhNode);

                if (parseResult.HeaderIsCompressed)
                {
                    nhNode.AddChild(MakeField("HeaderType", "编码头 (Encoded/LZMA 压缩)", 0, 0, FieldDataType.Struct, 0.8));

                    if (parseResult.PackStreams.Count > 0)
                    {
                        nhNode.AddChild(MakeField("PackStreams", $"{parseResult.PackStreams.Count} 个数据流", 0, 0, FieldDataType.Struct, 0.7));
                    }
                }
                else
                {
                    nhNode.AddChild(MakeField("HeaderType", "普通头 (Plain Header)", 0, 0, FieldDataType.Struct, 0.9));

                    if (parseResult.NumFiles > 0)
                    {
                        var numFilesNode = MakeField("NumFiles", $"{parseResult.NumFiles} 个文件", 0, 0, FieldDataType.Struct, 0.9);
                        nhNode.AddChild(numFilesNode);

                        if (parseResult.IsEncrypted)
                        {
                            nhNode.AddChild(MakeField("Encryption", "检测到 7zAES-256 加密", 0, 0, FieldDataType.Struct, 0.95));
                        }

                        if (parseResult.CompressionMethods != null)
                        {
                            nhNode.AddChild(MakeField("Compression", parseResult.CompressionMethods, 0, 0, FieldDataType.Struct, 0.8));
                        }

                        // 预览前 10 个文件名
                        int previewCount = Math.Min(parseResult.Files.Count, 10);
                        for (int i = 0; i < previewCount; i++)
                        {
                            var f = parseResult.Files[i];
                            var fileName = string.IsNullOrEmpty(f.Name) ? $"(entry_{i})" : f.Name;
                            var enc = f.IsEncrypted ? " [加密]" : "";
                            var info = $"{fileName}{enc}  {parseResult.CompressionMethods ?? "?"}";
                            nhNode.AddChild(MakeField($"File[{i}]", info, 0, 0, FieldDataType.Struct, 0.85));
                        }
                        if (parseResult.Files.Count > 10)
                        {
                            nhNode.AddChild(MakeField("...", $"...以及其他 {parseResult.Files.Count - 10} 个文件", 0, 0, FieldDataType.Struct, 0.7));
                        }
                    }
                }

                if (parseResult.ErrorMessage != null)
                {
                    nhNode.AddChild(MakeField("ParseWarning", parseResult.ErrorMessage, 0, 0, FieldDataType.Struct, 0.5));
                }
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>TAR 后处理：解析条目数量和首文件名</summary>
    private static void PostProcessTar(BinaryBuffer buffer, StructureNode tarNode)
    {
        try
        {
            if (buffer.Length < 512) return;
            var nameBytes = buffer.ReadBytes(0, 100);
            var firstName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
            if (!string.IsNullOrEmpty(firstName))
                tarNode.AddChild(MakeField("FirstFile", firstName, 0, 100, FieldDataType.ASCII, 0.9));
            int count = 0; long pos = 0;
            while (pos + 512 <= buffer.Length)
            {
                bool isZero = true;
                for (int i = 0; i < 512 && pos + i < buffer.Length; i++)
                    if (buffer.ReadByte(pos + i) != 0) { isZero = false; break; }
                if (isZero) break;
                count++;
                var sizeStr = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes(pos + 124, 12)).TrimEnd('\0');
                long fileSize = sizeStr.Length > 0 ? System.Convert.ToInt64(sizeStr, 8) : 0;
                pos += 512 + ((fileSize + 511) / 512) * 512;
                if (count > 100000) break;
            }
            if (count > 0)
                tarNode.AddChild(MakeField("FileCount", $"{count}", 0, 0, FieldDataType.Bytes, 0.85));
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>GZip 后处理：解析 FLG 可选字段、尾部 CRC32/ISIZE</summary>
    private static void PostProcessGzip(BinaryBuffer buffer, StructureNode gzNode)
    {
        try
        {
            if (buffer.Length < 18) return;
            var flg = buffer.ReadByte(3);
            int off = 10;
            if ((flg & 0x04) != 0 && off + 2 <= buffer.Length)
            { var xlen = buffer.ReadUInt16(off, true); off += 2 + xlen; }
            if ((flg & 0x08) != 0)
            {
                var ns = off; while (off < buffer.Length && buffer.ReadByte(off) != 0) off++;
                if (off > ns) gzNode.AddChild(MakeField("OriginalName", System.Text.Encoding.ASCII.GetString(buffer.ReadBytes(ns, off - ns)), ns, off - ns, FieldDataType.ASCII, 0.85));
                off++;
            }
            if ((flg & 0x10) != 0)
            {
                var cs = off; while (off < buffer.Length && buffer.ReadByte(off) != 0) off++;
                if (off > cs) gzNode.AddChild(MakeField("Comment", System.Text.Encoding.ASCII.GetString(buffer.ReadBytes(cs, off - cs)), cs, off - cs, FieldDataType.ASCII, 0.85));
                off++;
            }
            if ((flg & 0x02) != 0) off += 2;
            if (buffer.Length >= 8)
            {
                gzNode.AddChild(MakeField("CRC32", $"0x{buffer.ReadUInt32(buffer.Length - 8, true):X8}", buffer.Length - 8, 4, FieldDataType.UInt32LE, 0.9));
                gzNode.AddChild(MakeField("ISIZE", $"{buffer.ReadUInt32(buffer.Length - 4, true)} bytes", buffer.Length - 4, 4, FieldDataType.UInt32LE, 0.9));
            }
        }
        catch { /* 不中断识别 */ }
    }

    /// <summary>ZIP 后处理：扫描 EOCD 获取文件数量和 CD 偏移</summary>
    private static void PostProcessZip(BinaryBuffer buffer, StructureNode zipNode)
    {
        try
        {
            if (buffer.Length < 22) return;
            long eocdOff = -1;
            var tailSize = (int)Math.Min(buffer.Length, 0x100FF);
            var tail = buffer.ReadBytes(buffer.Length - tailSize, tailSize);
            for (int i = tail.Length - 22; i >= 0; i--)
                if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                { eocdOff = buffer.Length - tailSize + i; break; }
            if (eocdOff < 0) return;
            var totalEntries = buffer.ReadUInt16(eocdOff + 8, true);
            var cdOffset = buffer.ReadUInt32(eocdOff + 16, true);
            var cdDiskNum = buffer.ReadUInt16(eocdOff + 6, true);
            zipNode.AddChild(MakeField("EOCD", $"entries={totalEntries}, cdOff=0x{cdOffset:X}, disk={cdDiskNum}",
                eocdOff, 22, FieldDataType.Bytes, 0.9));
        }
        catch { /* 不中断识别 */ }
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
