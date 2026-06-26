using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class HeuristicEngine : IHeuristicEngine
{
    public async Task<StructureNode> InferAsync(BinaryBuffer buffer,
        IProgress<RecognitionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var root = new StructureNode
        {
            Name = "文件根",
            Offset = 0,
            Length = buffer.Length,
            DataType = FieldDataType.Bytes,
            Confidence = 1.0,
            Source = StructureNodeSource.AutoDetected,
        };

        progress?.Report(new RecognitionProgress(10, "分析字节分布..."));

        // 读取文件头部分用于分析（最多 64KB）
        var scanSize = (int)Math.Min(buffer.Length, 65536);
        var headerData = buffer.ReadBytes(0, scanSize);

        ct.ThrowIfCancellationRequested();

        // 1. 字节分布分析
        var distribution = AnalyzeDistribution(headerData);
        progress?.Report(new RecognitionProgress(30, $"字节分布分析完成 (熵值: {distribution.Entropy:F2})"));

        ct.ThrowIfCancellationRequested();

        // 2. 重复块检测
        var repeatingBlocks = await Task.Run(() =>
            DetectRepeatingBlocks(buffer, scanSize), ct);
        progress?.Report(new RecognitionProgress(60, $"发现 {repeatingBlocks.Count} 个重复块"));

        ct.ThrowIfCancellationRequested();

        // 3. 字段边界检测
        var boundaries = await Task.Run(() =>
            DetectFieldBoundaries(headerData), ct);
        progress?.Report(new RecognitionProgress(80, $"检测到 {boundaries.Count} 个潜在字段边界"));

        // 组装结果
        foreach (var block in repeatingBlocks.Take(10))
        {
            var blockNode = new StructureNode
            {
                Name = $"重复块 @ {block.Offset:X}",
                Offset = block.Offset,
                Length = block.Length,
                DataType = FieldDataType.Bytes,
                Confidence = block.Confidence,
                Source = StructureNodeSource.AutoDetected,
            };
            root.AddChild(blockNode);
        }

        foreach (var boundary in boundaries.Take(20))
        {
            // 跳过已被重复块覆盖的区域
            if (repeatingBlocks.Any(b => boundary.Offset >= b.Offset && boundary.Offset < b.Offset + b.Length))
                continue;

            var fieldNode = new StructureNode
            {
                Name = $"字段 @ {boundary.Offset:X}",
                Offset = boundary.Offset,
                Length = boundary.Length,
                DataType = GuessDataType(headerData, boundary),
                Confidence = boundary.Confidence,
                Source = StructureNodeSource.AutoDetected,
            };
            root.AddChild(fieldNode);
        }

        progress?.Report(new RecognitionProgress(100, "启发式分析完成"));

        return root;
    }

    private ByteDistributionResult AnalyzeDistribution(byte[] data)
    {
        var histogram = new long[256];
        foreach (var b in data) histogram[b]++;

        double entropy = 0;
        foreach (var count in histogram)
        {
            if (count == 0) continue;
            var p = (double)count / data.Length;
            entropy -= p * Math.Log2(p);
        }

        return new ByteDistributionResult
        {
            Entropy = entropy,
            Histogram = histogram,
            IsHighEntropy = entropy > 7.0,
            IsLowEntropy = entropy < 4.0,
        };
    }

    private List<BlockResult> DetectRepeatingBlocks(BinaryBuffer buffer, int scanSize)
    {
        var results = new List<BlockResult>();
        var stepSizes = new[] { 4, 8, 16, 32, 64, 128, 256 };

        foreach (var step in stepSizes)
        {
            if (step * 3 > scanSize) continue;

            var matchCount = 0;
            var totalComparisons = 0;

            var firstBlock = buffer.ReadBytes(0, step);
            for (int offset = step; offset + step <= scanSize; offset += step)
            {
                totalComparisons++;
                var nextBlock = buffer.ReadBytes(offset, step);

                var differences = 0;
                for (int i = 0; i < step; i++)
                    if (firstBlock[i] != nextBlock[i]) differences++;

                if (differences == 0) matchCount++;
            }

            if (totalComparisons > 0 && (double)matchCount / totalComparisons > 0.5)
            {
                var confidence = Math.Min(0.9, (double)matchCount / totalComparisons);
                results.Add(new BlockResult
                {
                    Offset = 0,
                    Length = step,
                    BlockSize = step,
                    Confidence = confidence,
                    MatchCount = matchCount,
                });
            }
        }

        return results;
    }

    private List<BoundaryResult> DetectFieldBoundaries(byte[] data)
    {
        var results = new List<BoundaryResult>();
        var alignments = new[] { 2, 4, 8 };

        foreach (var alignment in alignments)
        {
            for (int offset = alignment; offset + 4 <= data.Length; offset += alignment)
            {
                // 检查对齐边界处是否有值突变
                var before = BitConverter.ToUInt32(data, offset - alignment);
                var after = BitConverter.ToUInt32(data, offset);

                if (Math.Abs(before - after) > uint.MaxValue * 0.8)
                {
                    results.Add(new BoundaryResult
                    {
                        Offset = offset,
                        Length = alignment,
                        Confidence = 0.4,
                        Alignment = alignment,
                    });
                }
            }
        }

        return results;
    }

    private static FieldDataType GuessDataType(byte[] data, BoundaryResult boundary)
    {
        if (boundary.Length >= 8) return FieldDataType.UInt64LE;
        if (boundary.Length >= 4) return FieldDataType.UInt32LE;
        if (boundary.Length >= 2) return FieldDataType.UInt16LE;
        return FieldDataType.UInt8;
    }

    private struct ByteDistributionResult
    {
        public double Entropy;
        public long[] Histogram;
        public bool IsHighEntropy;
        public bool IsLowEntropy;
    }

    private struct BlockResult
    {
        public long Offset;
        public long Length;
        public int BlockSize;
        public double Confidence;
        public int MatchCount;
    }

    private struct BoundaryResult
    {
        public long Offset;
        public long Length;
        public double Confidence;
        public int Alignment;
    }
}
