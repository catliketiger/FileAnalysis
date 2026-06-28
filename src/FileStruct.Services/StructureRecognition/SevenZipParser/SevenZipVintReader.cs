namespace FileStruct.Services.StructureRecognition.SevenZipParser;

/// <summary>7z 格式变长整数 (VINT) 解码器</summary>
/// <remarks>
/// 参考 7zFormat.txt 的编码规约:
///   First_Byte         Extra_Bytes    Value
///   0xxxxxxx                          (x)
///   10xxxxxx           BYTE y[1]      (xx << 8) + y
///   110xxxxx           BYTE y[2]      (xxx << 16) + y
///   ...
///   11111110           BYTE y[7]      y (8字节)
///   11111111           BYTE y[8]      y (9字节)
/// </remarks>
public static class SevenZipVintReader
{
    /// <summary>从 byte[] 的指定偏移读取一个 7z 变长 UINT64</summary>
    /// <returns>(解码值, 消耗字节数)</returns>
    public static (ulong value, int bytesConsumed) ReadVint(byte[] data, int offset)
    {
        if (offset >= data.Length)
            throw new EndOfStreamException("VINT 读取越界");

        byte first = data[offset];

        // 0xxxxxxx → 单字节，7-bit 值
        if ((first & 0x80) == 0)
            return (first, 1);

        // 统计前缀 1 的位数 = extraBytes
        int extraBytes = 0;
        byte mask = 0x80;
        while ((first & mask) != 0 && extraBytes < 8)
        {
            extraBytes++;
            mask >>= 1;
        }

        // 前缀后面的数据位: 7 - extraBytes 位（0xFF 时 extraBytes=8, dataBits=-1 → 0）
        int dataBits = 7 - extraBytes;
        ulong result = dataBits > 0 ? (uint)(first & ((1 << dataBits) - 1)) : 0;

        // 额外字节以小端序拼接: y = Σ(extra[i] << 8*i)
        ulong y = 0;
        for (int i = 0; i < extraBytes; i++)
        {
            int pos = offset + 1 + i;
            if (pos >= data.Length)
                throw new EndOfStreamException("VINT 额外字节读取越界");
            y |= (ulong)data[pos] << (8 * i);
        }

        result = (result << (8 * extraBytes)) + y;

        return (result, extraBytes + 1);
    }

    /// <summary>跳过变长整数，返回新的偏移</summary>
    public static int SkipVint(byte[] data, int offset)
    {
        var (_, consumed) = ReadVint(data, offset);
        return offset + consumed;
    }

    /// <summary>读取 REAL_UINT64（固定 8 字节小端）</summary>
    public static ulong ReadRealUInt64(byte[] data, int offset)
    {
        if (offset + 8 > data.Length)
            throw new EndOfStreamException("REAL_UINT64 读取越界");
        ulong result = 0;
        for (int i = 0; i < 8; i++)
            result |= (ulong)data[offset + i] << (8 * i);
        return result;
    }
}
