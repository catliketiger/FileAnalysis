namespace FileStruct.Services.StructureRecognition.SevenZipParser;

/// <summary>7z Codec ID → 可读字符串映射器</summary>
/// <remarks>
/// 参考 Methods.txt 中定义的编码器 ID 表。
/// ID 为变长字节序列，最长 8 字节。
/// </remarks>
public static class SevenZipMethodMapper
{
    private static readonly (byte[] id, string name, bool isEncryption)[] KnownMethods =
    [
        (new byte[] { 0x00 },                          "Copy",     false),
        (new byte[] { 0x03 },                          "Delta",    false),
        (new byte[] { 0x21 },                          "LZMA2",    false),
        (new byte[] { 0x03, 0x01, 0x01 },              "LZMA",     false),
        (new byte[] { 0x03, 0x03, 0x01, 0x03 },        "BCJ",      false),
        (new byte[] { 0x03, 0x03, 0x01, 0x1B },        "BCJ2",     false),
        (new byte[] { 0x03, 0x04, 0x01 },              "PPMD",     false),
        (new byte[] { 0x04, 0x01, 0x08 },              "Deflate",  false),
        (new byte[] { 0x04, 0x01, 0x5D },              "ZSTD",     false),
        (new byte[] { 0x04, 0x02, 0x02 },              "BZip2",    false),
        (new byte[] { 0x06, 0xF1, 0x07, 0x01 },        "7zAES",    true),
        (new byte[] { 0x06, 0xF1, 0x01, 0x01 },        "ZipCrypto",true),
        (new byte[] { 0x04, 0x01, 0x60 },              "JPEG",     false),
        (new byte[] { 0x04, 0x01, 0x61 },              "WavPack",  false),
        (new byte[] { 0x04, 0x01, 0x62 },              "PPMd(ZIP)",false),
        (new byte[] { 0x04, 0x01, 0x63 },              "wzAES",    true),
        (new byte[] { 0x04, 0x01, 0x5F },              "xz",       false),
        (new byte[] { 0x04, 0xF7, 0x10, 0x01 },        "LZHAM",    false),
    ];

    /// <summary>将 Codec ID 字节序列映射为可读名称</summary>
    public static string Map(byte[] coderId)
    {
        foreach (var (id, name, _) in KnownMethods)
        {
            if (id.Length != coderId.Length) continue;
            bool match = true;
            for (int i = 0; i < id.Length; i++)
            {
                if (id[i] != coderId[i]) { match = false; break; }
            }
            if (match) return name;
        }

        // 未知编码器：格式化为 hex
        return $"0x{string.Join("", coderId.Select(b => $"{b:X2}"))}";
    }

    /// <summary>判断 Codec ID 是否为加密方法</summary>
    public static bool IsEncryption(byte[] coderId)
    {
        foreach (var (id, _, isEnc) in KnownMethods)
        {
            if (id.Length != coderId.Length) continue;
            bool match = true;
            for (int i = 0; i < id.Length; i++)
            {
                if (id[i] != coderId[i]) { match = false; break; }
            }
            if (match) return isEnc;
        }
        return false;
    }
}
