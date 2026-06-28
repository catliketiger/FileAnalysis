namespace FileStruct.Services.StructureRecognition.SevenZipParser;

/// <summary>
/// LZMA 单次解压薄封装。内部使用 LZMA SDK (public domain) 的 Decoder。
/// 将 byte[] 适配为 MemoryStream 调用 SDK 的 Code() 方法。
/// </summary>
public static class SevenZipLzmaDecoder
{
    /// <summary>LZMA 单次解压 (RAM→RAM)</summary>
    /// <param name="props">5 字节 LZMA 属性</param>
    /// <param name="inData">压缩数据（纯 LZMA 流，无文件头）</param>
    /// <param name="unpackedSize">预期解压后大小</param>
    public static byte[] Decompress(byte[] props, byte[] inData, int unpackedSize)
    {
        var decoder = new SevenZip.Compression.LZMA.Decoder();
        decoder.SetDecoderProperties(props);

        using var inStream = new MemoryStream(inData, writable: false);
        using var outStream = new MemoryStream(new byte[unpackedSize], writable: true);

        decoder.Code(inStream, outStream, inData.Length, unpackedSize, null);

        return outStream.ToArray();
    }
}
