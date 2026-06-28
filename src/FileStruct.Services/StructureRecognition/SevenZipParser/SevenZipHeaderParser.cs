using System.Text;

namespace FileStruct.Services.StructureRecognition.SevenZipParser;

/// <summary>
/// 7z NextHeader NID 树解析器。
/// 严格遵循 7zFormat.txt 的协议 ID 编码规约。
/// </summary>
/// <remarks>
/// 解析流程：
///   入口 Parse() → 识别 kHeader(0x01) / kEncodedHeader(0x17)
///     → 递归遍历 StreamsInfo (PackInfo/CodersInfo/SubStreamsInfo)
///     → 解析 FilesInfo (NumFiles + Property 块)
///     → 提取文件名 (kName)、检测加密 (7zAES)
/// </remarks>
public class SevenZipHeaderParser
{
    private const int MaxNidIterations = 10000;

    /// <summary>解析完整的 NextHeader 数据</summary>
    public SevenZipParseResult Parse(byte[] data)
    {
        var result = new SevenZipParseResult();
        int pos = 0;
        int iterations = 0;

        try
        {
            while (pos < data.Length && iterations < MaxNidIterations)
            {
                iterations++;
                if (pos >= data.Length) break;

                byte nid = data[pos++];

                if (nid == 0x00) // kEnd
                    break;

                switch (nid)
                {
                    case 0x01: // kHeader
                        ParseHeaderBody(data, ref pos, result);
                        break;

                    case 0x17: // kEncodedHeader
                        result.HeaderIsCompressed = true;
                        // EncodedHeader 本身是一个 StreamsInfo — 解析以获取 PackStream 结构信息
                        ParseStreamsInfo(data, ref pos, result);
                        break;

                    default:
                        // 根级别的未知 NID — 跳过
                        break;
                }
            }

            if (iterations >= MaxNidIterations)
                result.ErrorMessage = "NID 迭代超过上限，数据可能已损坏";
        }
        catch (EndOfStreamException ex)
        {
            result.ErrorMessage = $"7z 头解析截断: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"7z 头解析异常: {ex.Message}";
        }

        return result;
    }

    // =================================================================
    //  Header Body  (kHeader 0x01 下的三个可选节)
    // =================================================================

    private void ParseHeaderBody(byte[] data, ref int pos, SevenZipParseResult result)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;
            byte nid = data[pos++];

            if (nid == 0x00) // kEnd
                return;

            switch (nid)
            {
                case 0x02: // kArchiveProperties
                    SkipProperties(data, ref pos);
                    break;

                case 0x03: // kAdditionalStreamsInfo
                    SkipStreamsInfo(data, ref pos);
                    break;

                case 0x04: // kMainStreamsInfo
                    ParseStreamsInfo(data, ref pos, result);
                    break;

                case 0x05: // kFilesInfo
                    ParseFilesInfo(data, ref pos, result);
                    break;

                default:
                    // 未知 NID — 尝试跳过
                    SkipUnknownProperty(data, ref pos, nid);
                    break;
            }
        }
    }

    // =================================================================
    //  StreamsInfo
    // =================================================================

    private void ParseStreamsInfo(byte[] data, ref int pos, SevenZipParseResult result)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;
            byte nid = data[pos++];

            if (nid == 0x00) return; // kEnd

            switch (nid)
            {
                case 0x06: // kPackInfo
                    ParsePackInfo(data, ref pos, result);
                    break;

                case 0x07: // kUnPackInfo (CodersInfo)
                    ParseCodersInfo(data, ref pos, result);
                    break;

                case 0x08: // kSubStreamsInfo
                    ParseSubStreamsInfo(data, ref pos, result);
                    break;

                default:
                    SkipUnknownProperty(data, ref pos, nid);
                    break;
            }
        }
    }

    private static void SkipStreamsInfo(byte[] data, ref int pos)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;
            byte nid = data[pos++];
            if (nid == 0x00) return;

            switch (nid)
            {
                case 0x06: // kPackInfo
                    SkipToNextNid(data, ref pos);
                    break;
                case 0x07: // kUnPackInfo
                    SkipToNextNid(data, ref pos);
                    break;
                case 0x08: // kSubStreamsInfo
                    SkipToNextNid(data, ref pos);
                    break;
                default:
                    SkipUnknownProperty(data, ref pos, nid);
                    break;
            }
        }
    }

    // =================================================================
    //  PackInfo  (0x06)
    //
    //  kPackInfo
    //  UINT64 PackPos
    //  UINT64 NumPackStreams
    //  [kSize, PackSizes[NumPackStreams]]
    //  [kCRC, PackStreamDigests[NumPackStreams]]
    //  kEnd
    // =================================================================

    private void ParsePackInfo(byte[] data, ref int pos, SevenZipParseResult result)
    {
        if (pos >= data.Length) return;

        // PackPos
        var (packPos, consumed1) = SevenZipVintReader.ReadVint(data, pos);
        pos += consumed1;

        // NumPackStreams
        var (numStreams, consumed2) = SevenZipVintReader.ReadVint(data, pos);
        pos += consumed2;

        var streamSizes = new List<ulong>();

        // 子属性
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;
            byte subNid = data[pos++];

            if (subNid == 0x00) break; // kEnd

            switch (subNid)
            {
                case 0x09: // kSize
                    for (ulong i = 0; i < numStreams && pos < data.Length; i++)
                    {
                        var (size, consumed) = SevenZipVintReader.ReadVint(data, pos);
                        pos += consumed;
                        streamSizes.Add(size);
                    }
                    break;

                case 0x0A: // kCRC
                    SkipDigests(data, ref pos, (int)numStreams);
                    break;

                default:
                    SkipUnknownProperty(data, ref pos, subNid);
                    break;
            }
        }

        // 填充 PackStreams
        ulong cumulativePos = (ulong)(long)packPos;
        for (int i = 0; i < streamSizes.Count; i++)
        {
            result.PackStreams.Add(new PackStreamInfo
            {
                PackPos = (long)cumulativePos,
                PackSize = (long)streamSizes[i],
            });
            cumulativePos += streamSizes[i];
        }
    }

    // =================================================================
    //  CodersInfo (kUnPackInfo 0x07)
    //
    //  kUnPackInfo
    //  kFolder (0x0B): NumFolders, External, Folders[...]
    //  kCodersUnPackSize (0x0C): unpack sizes
    //  [kCRC]: digests
    //  kEnd
    // =================================================================

    private void ParseCodersInfo(byte[] data, ref int pos, SevenZipParseResult result)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;

            byte subNid = data[pos++];
            if (subNid == 0x00) return; // kEnd

            switch (subNid)
            {
                case 0x0B: // kFolder
                    ParseFolders(data, ref pos, result);
                    break;

                case 0x0C: // kCodersUnPackSize
                    // 读取第一个 Folder 的第一个输出流解压大小
                    if (result.HeaderUnpackedSize == 0 && pos < data.Length)
                    {
                        var (unpackSize, consumed) = SevenZipVintReader.ReadVint(data, pos);
                        result.HeaderUnpackedSize = (int)unpackSize;
                    }
                    SkipVintArray(data, ref pos);
                    break;

                case 0x0A: // kCRC
                    // 有 NumFolders 个 digest，但我们不知道 NumFolders
                    // 跳过剩余到下一个 NID
                    SkipToNextNid(data, ref pos);
                    break;

                default:
                    SkipUnknownProperty(data, ref pos, subNid);
                    break;
            }
        }
    }

    private void ParseFolders(byte[] data, ref int pos, SevenZipParseResult result)
    {
        if (pos >= data.Length) return;
        var (numFolders, consumed) = SevenZipVintReader.ReadVint(data, pos);
        pos += consumed;

        result.NumFolders = (int)numFolders;

        if (pos >= data.Length) return;
        byte external = data[pos++];

        if (external != 0)
        {
            // 外部索引 — 跳过
            var (_, c) = SevenZipVintReader.ReadVint(data, pos);
            pos += c;
            return;
        }

        var methods = new List<string>();

        for (ulong f = 0; f < numFolders; f++)
        {
            ParseFolder(data, ref pos, result, methods);
        }

        if (methods.Count > 0)
            result.CompressionMethods = string.Join("+", methods.Distinct());
    }

    private void ParseFolder(byte[] data, ref int pos, SevenZipParseResult result, List<string> methods)
    {
        if (pos >= data.Length) return;

        // NumCoders
        var (numCoders, consumed) = SevenZipVintReader.ReadVint(data, pos);
        pos += consumed;

        ulong totalInStreams = 0;
        ulong totalOutStreams = 0;

        for (ulong c = 0; c < numCoders; c++)
        {
            if (pos >= data.Length) return;
            byte flags = data[pos++];

            int codecIdSize = flags & 0x0F;
            bool isComplex = (flags & 0x10) != 0;
            bool hasAttributes = (flags & 0x20) != 0;

            // CodecId
            if (pos + codecIdSize > data.Length) return;
            var codecId = new byte[codecIdSize];
            Array.Copy(data, pos, codecId, 0, codecIdSize);
            pos += codecIdSize;

            string method = SevenZipMethodMapper.Map(codecId);
            methods.Add(method);

            if (SevenZipMethodMapper.IsEncryption(codecId))
                result.IsEncrypted = true;

            if (isComplex)
            {
                var (numIn, c1) = SevenZipVintReader.ReadVint(data, pos);
                pos += c1;
                var (numOut, c2) = SevenZipVintReader.ReadVint(data, pos);
                pos += c2;
                totalInStreams += numIn;
                totalOutStreams += numOut;
            }
            else
            {
                totalInStreams += 1;
                totalOutStreams += 1;
            }

            if (hasAttributes)
            {
                var (propSize64, c3) = SevenZipVintReader.ReadVint(data, pos);
                pos += c3;
                int propSize = (int)propSize64;

                // 捕获第一个编码器的 LZMA 属性（用于 EncodedHeader 解压）
                if (result.LzmaProperties == null && propSize >= 5 && pos + propSize <= data.Length)
                {
                    result.LzmaProperties = new byte[Math.Min(propSize, 5)];
                    Array.Copy(data, pos, result.LzmaProperties, 0, result.LzmaProperties.Length);
                }

                pos += propSize;
                if (pos > data.Length) pos = data.Length;
            }
        }

        // BindPairs
        ulong numBindPairs = totalOutStreams > 0 ? totalOutStreams - 1 : 0;
        for (ulong bp = 0; bp < numBindPairs; bp++)
        {
            if (pos >= data.Length) return;
            var (_, c1) = SevenZipVintReader.ReadVint(data, pos);
            pos += c1;
            if (pos >= data.Length) return;
            var (_, c2) = SevenZipVintReader.ReadVint(data, pos);
            pos += c2;
        }

        // PackedIndices
        ulong numPackedStreams = totalInStreams > numBindPairs ? totalInStreams - numBindPairs : 0;
        if (numPackedStreams > 1)
        {
            for (ulong pi = 0; pi < numPackedStreams; pi++)
            {
                if (pos >= data.Length) return;
                var (_, c) = SevenZipVintReader.ReadVint(data, pos);
                pos += c;
            }
        }
    }

    // =================================================================
    //  SubStreamsInfo  (0x08)
    // =================================================================

    private static void ParseSubStreamsInfo(byte[] data, ref int pos, SevenZipParseResult result)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) return;
            byte subNid = data[pos++];
            if (subNid == 0x00) return; // kEnd of SubStreamsInfo

            switch (subNid)
            {
                case 0x0D: // kNumUnPackStream — NumFolders 个 VINT
                    for (int i = 0; i < result.NumFolders && pos < data.Length; i++)
                    {
                        var (_, c) = SevenZipVintReader.ReadVint(data, pos);
                        pos += c;
                    }
                    break;

                case 0x09: // kSize — VINT 数组（单个文件的解压大小），读到下一个 NID 为止
                    while (pos < data.Length)
                    {
                        byte b = data[pos];
                        if (b == 0x00) break;  // kEnd — 不消费
                        if (b < 0x20) break;   // 其他 NID — 不消费
                        var (size, c) = SevenZipVintReader.ReadVint(data, pos);
                        pos += c;
                        result.SubStreamUnpackSizes.Add((long)size);
                    }
                    break;

                case 0x0A: // kCRC — SubStreamsInfo 中总是最后一个属性，读到 kEnd
                    SkipToKEnd(data, ref pos);
                    return; // SubStreamsInfo ends after kCRC

                default:
                    SkipUnknownProperty(data, ref pos, subNid);
                    break;
            }
        }
    }

    // =================================================================
    //  FilesInfo  (0x05)
    //
    //  kFilesInfo
    //  UINT64 NumFiles
    //  for (;;) {
    //    BYTE PropertyType (0 = kEnd)
    //    UINT64 Size
    //    Data[Size]
    //  }
    // =================================================================

    private void ParseFilesInfo(byte[] data, ref int pos, SevenZipParseResult result)
    {
        if (pos >= data.Length) return;

        // NumFiles
        var (numFiles, consumed) = SevenZipVintReader.ReadVint(data, pos);
        pos += consumed;

        result.NumFiles = (int)Math.Min(numFiles, 1000000L);

        // 预填充空条目（等待 kName 填充名称）
        for (int i = 0; i < result.NumFiles; i++)
        {
            result.Files.Add(new SevenZipFileEntry());
        }

        // 标记空文件的索引
        var emptyStreams = new HashSet<int>();

        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            if (pos >= data.Length) break;

            byte propType = data[pos++];
            if (propType == 0x00) break; // kEnd

            if (pos >= data.Length) break;
            var (propSize, sizeConsumed) = SevenZipVintReader.ReadVint(data, pos);
            pos += sizeConsumed;
            long propEnd = pos + (long)propSize;
            if (propEnd > data.Length) propEnd = data.Length;

            switch (propType)
            {
                case 0x0E: // kEmptyStream — BIT array
                    ParseBitfield(data, ref pos, (int)propSize, result.NumFiles, emptyStreams);
                    break;

                case 0x11: // kName
                    ParseFileNames(data, ref pos, (int)propSize, result);
                    break;

                case 0x14: // kMTime
                    ParseTimestamps(data, ref pos, (int)propSize, result, t =>
                    {
                        // 不阻塞解析，跳过具体值
                    });
                    break;

                case 0x15: // kWinAttributes
                case 0x12: // kCTime
                case 0x13: // kATime
                case 0x0F: // kEmptyFile
                case 0x10: // kAnti
                default:
                    // 跳过
                    pos = (int)propEnd;
                    break;
            }
        }

        // 标记空流条目
        foreach (int idx in emptyStreams)
        {
            if (idx < result.Files.Count)
                result.Files[idx].IsEmptyStream = true;
        }
    }

    // =================================================================
    //  文件名解析（kName 0x11）
    //
    //  kName:
    //    BYTE External
    //    if External == 0:
    //      for(NumFiles) { wchar_t null-terminated string }
    //    if External != 0:
    //      UINT64 DataStreamIndex
    // =================================================================

    private static void ParseFileNames(byte[] data, ref int pos, int propSize, SevenZipParseResult result)
    {
        if (pos >= data.Length) return;

        byte external = data[pos++];
        int remaining = propSize - 1;
        if (remaining <= 0) return;

        if (external != 0)
        {
            // 外部流索引 — 跳过剩余
            pos += remaining;
            return;
        }

        // 内联文件名：UTF-16LE、null 终止
        // 剩余字节数是整个名称数据块的大小
        int nameDataEnd = pos + remaining;
        if (nameDataEnd > data.Length) nameDataEnd = data.Length;

        int fileIndex = 0;
        int offset = pos;

        while (offset + 1 < nameDataEnd && fileIndex < result.Files.Count)
        {
            // UTF-16LE 字符序列直到双 0x00
            int start = offset;
            while (offset + 1 < nameDataEnd)
            {
                if (data[offset] == 0 && data[offset + 1] == 0)
                    break;
                offset += 2;
            }

            int nameLen = offset - start;
            if (nameLen > 0)
            {
                string name = Encoding.Unicode.GetString(data, start, nameLen);
                result.Files[fileIndex].Name = name;
            }

            // 跳过 null 终止符 (2 字节)
            offset += 2;
            fileIndex++;
        }

        pos = nameDataEnd;
    }

    // =================================================================
    //  工具方法
    // =================================================================

    /// <summary>解析 BIT 字段（MSB-first: 每字节 bit7=第0个文件, bit6=第1个...）</summary>
    private static void ParseBitfield(byte[] data, ref int pos, int size, int numBits, HashSet<int> setBits)
    {
        int bytesToRead = Math.Min(size, (numBits + 7) / 8);
        for (int i = 0; i < bytesToRead && pos < data.Length; i++)
        {
            byte b = data[pos++];
            for (int bit = 0; bit < 8; bit++)
            {
                int index = i * 8 + bit;
                if (index >= numBits) break;
                // MSB-first: bit 7 → index 0, bit 6 → index 1, ...
                if ((b & (0x80 >> bit)) != 0)
                    setBits.Add(index);
            }
        }
        pos = Math.Min(pos + (size - bytesToRead), data.Length);
    }

    /// <summary>跳过摘要（AllAreDefined + BIT 定义 + CRC32 数组）</summary>
    private static void SkipDigests(byte[] data, ref int pos, int numStreams)
    {
        if (pos >= data.Length) return;
        byte allDefined = data[pos++];

        int numDefined;
        if (allDefined == 0)
        {
            // 统计 BIT 位图中为 1 的位数
            int bitBytes = (numStreams + 7) / 8;
            numDefined = 0;
            for (int i = 0; i < bitBytes && pos < data.Length; i++)
            {
                byte b = data[pos++];
                // 快速统计置位 bit 数
                numDefined += System.Numerics.BitOperations.PopCount((uint)b);
            }
        }
        else
        {
            numDefined = numStreams;
        }

        // 跳过 CRC32 值（每个 4 字节）
        pos += numDefined * 4;
        if (pos > data.Length) pos = data.Length;
    }

    /// <summary>跳过 ArchiveProperties</summary>
    private static void SkipProperties(byte[] data, ref int pos)
    {
        int iterations = 0;
        while (pos < data.Length && iterations < 1000)
        {
            iterations++;
            byte pt = data[pos++];
            if (pt == 0) return;
            var (size, consumed) = SevenZipVintReader.ReadVint(data, pos);
            pos += consumed + (int)size;
        }
    }

    /// <summary>跳过 UINT64 数组直到遇到 NID 标记</summary>
    private static void SkipVintArray(byte[] data, ref int pos)
    {
        while (pos < data.Length)
        {
            byte b = data[pos];
            if (b == 0x00) return;  // kEnd — 停止，不消费
            if (b < 0x20) return;   // 其他 NID — 停止，不消费
            var (_, consumed) = SevenZipVintReader.ReadVint(data, pos);
            pos += consumed;
        }
    }

    /// <summary>跳过未知属性到下一个 NID 标记</summary>
    private static void SkipUnknownProperty(byte[] data, ref int pos, byte currentNid)
    {
        // 对于大部分属性，后面跟 UINT64 size + data
        // 但对于某些 NID 可能不是 —— 保守做法：跳到下一个已知 NID
        if (pos >= data.Length) return;

        // 尝试读取 Size 域
        try
        {
            var (size, consumed) = SevenZipVintReader.ReadVint(data, pos);
            pos += consumed;
            pos += (int)Math.Min(size, (ulong)(data.Length - pos));
        }
        catch
        {
            // 无法解析安全跳过
        }
    }

    /// <summary>跳到下一个 kEnd 标记</summary>
    private static void SkipToNextNid(byte[] data, ref int pos)
    {
        int depth = 0;
        int iterations = 0;
        while (pos < data.Length && iterations < MaxNidIterations)
        {
            iterations++;
            byte b = data[pos++];
            if (b == 0x00) // kEnd
            {
                if (depth == 0) return;
                depth--;
            }
            // 当遇到 kHeader/kEncodedHeader，增加嵌套深度
            else if (b == 0x01 || b == 0x17)
            {
                depth++;
            }
        }
    }

    /// <summary>跳过数据直到遇到 0x00（kEnd），消费 kEnd 字节</summary>
    private static void SkipToKEnd(byte[] data, ref int pos)
    {
        // kCRC 格式: AllAreDefined(1) [+BIT bitfield] + CRC32[]. 之后应当跟 kEnd(0x00)
        // 直接扫描到下一个 0x00 并消费
        while (pos < data.Length)
        {
            if (data[pos] == 0x00) { pos++; return; }
            pos++;
        }
    }

    private static void ParseTimestamps(byte[] data, ref int pos, int propSize, SevenZipParseResult result, Action<long> onTime)
    {
        // 跳过 timestamps（实现非关键，仅用于解析不阻塞）
        pos += propSize;
        if (pos > data.Length) pos = data.Length;
    }
}
