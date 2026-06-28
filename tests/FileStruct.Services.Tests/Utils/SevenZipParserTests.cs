using System.Text;
using FileStruct.Services.StructureRecognition.SevenZipParser;
using Xunit;

namespace FileStruct.Services.Tests.Utils;

/// <summary>7z 解析器组件单元测试</summary>
public class SevenZipParserTests
{
    // =================================================================
    //  VINT 读取器测试
    // =================================================================

    [Fact]
    public void ReadVint_SingleByte_ReturnsValue()
    {
        byte[] data = [0x7F]; // 0xxxxxxx → 127
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(127UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void ReadVint_SingleByteZero_ReturnsZero()
    {
        byte[] data = [0x00];
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(0UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void ReadVint_TwoBytes_DecodesCorrectly()
    {
        // 10xxxxxx + 1 extra byte
        // first=0x82=10000010, extraBytes=1, mask=0x40, result=2<<8|0x34=0x234=564
        byte[] data = [0x82, 0x34];
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(564UL, value);
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void ReadVint_ThreeBytes_DecodesCorrectly()
    {
        // 110xxxxx + 2 extra bytes
        // first=0xC0=11000000, extraBytes=2, mask=0x20, result=0
        byte[] data = [0xC0, 0x02, 0x01];  // 额外字节小端序
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(0x102UL, value);
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void ReadVint_FourBytes_DecodesCorrectly()
    {
        // 1110xxxx + 3 extra bytes
        // first=0xE0=11100000, extraBytes=3, mask=0x10, result=0
        byte[] data = [0xE0, 0x03, 0x02, 0x01];  // 额外字节小端序
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(0x010203UL, value);
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void ReadVint_EightByte_DecodesCorrectly()
    {
        // 11111110 + 7 extra bytes = 8 bytes total
        byte[] data = [0xFE, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01];  // 额外字节小端序
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(0x01020304050607UL, value);
        Assert.Equal(8, consumed);
    }

    [Fact]
    public void ReadVint_NineByte_DecodesCorrectly()
    {
        // 11111111 + 8 extra bytes = 9 bytes total
        byte[] data = [0xFF, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01];  // 额外字节小端序
        var (value, consumed) = SevenZipVintReader.ReadVint(data, 0);
        Assert.Equal(0x0102030405060708UL, value);
        Assert.Equal(9, consumed);
    }

    [Fact]
    public void ReadVint_ThrowsOnEmptyData()
    {
        byte[] data = [];
        Assert.Throws<EndOfStreamException>(() => SevenZipVintReader.ReadVint(data, 0));
    }

    [Fact]
    public void ReadVint_ThrowsOnTruncatedMultiByte()
    {
        byte[] data = [0xC0, 0x01]; // Expected 3 bytes but only 2 available
        Assert.Throws<EndOfStreamException>(() => SevenZipVintReader.ReadVint(data, 0));
    }

    [Fact]
    public void SkipVint_AdvancesPosition()
    {
        byte[] data = [0x7F, 0x00];
        int newPos = SevenZipVintReader.SkipVint(data, 0);
        Assert.Equal(1, newPos);
    }

    [Fact]
    public void SkipVint_AdvancesPosition_MultiByte()
    {
        byte[] data = [0xC0, 0x01, 0x02, 0x00];
        int newPos = SevenZipVintReader.SkipVint(data, 0);
        Assert.Equal(3, newPos);
    }

    [Fact]
    public void ReadRealUInt64_Reads8BytesLittleEndian()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        ulong value = SevenZipVintReader.ReadRealUInt64(data, 0);
        Assert.Equal(0x0807060504030201UL, value);
    }

    [Fact]
    public void ReadRealUInt64_ThrowsOnTruncated()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        Assert.Throws<EndOfStreamException>(() => SevenZipVintReader.ReadRealUInt64(data, 4));
    }

    // =================================================================
    //  方法映射器测试
    // =================================================================

    [Fact]
    public void MethodMapper_CopyId_ReturnsCopy()
    {
        Assert.Equal("Copy", SevenZipMethodMapper.Map([0x00]));
    }

    [Fact]
    public void MethodMapper_LzmaId_ReturnsLZMA()
    {
        Assert.Equal("LZMA", SevenZipMethodMapper.Map([0x03, 0x01, 0x01]));
    }

    [Fact]
    public void MethodMapper_Lzma2Id_ReturnsLZMA2()
    {
        Assert.Equal("LZMA2", SevenZipMethodMapper.Map([0x21]));
    }

    [Fact]
    public void MethodMapper_7zAesId_Returns7zAES()
    {
        Assert.Equal("7zAES", SevenZipMethodMapper.Map([0x06, 0xF1, 0x07, 0x01]));
    }

    [Fact]
    public void MethodMapper_7zAesId_IsEncryption()
    {
        Assert.True(SevenZipMethodMapper.IsEncryption([0x06, 0xF1, 0x07, 0x01]));
    }

    [Fact]
    public void MethodMapper_CopyId_NotEncryption()
    {
        Assert.False(SevenZipMethodMapper.IsEncryption([0x00]));
    }

    [Fact]
    public void MethodMapper_UnknownId_ReturnsHexString()
    {
        var result = SevenZipMethodMapper.Map([0xAB, 0xCD, 0xEF]);
        Assert.Equal("0xABCDEF", result);
    }

    [Fact]
    public void MethodMapper_DeflateId_ReturnsDeflate()
    {
        Assert.Equal("Deflate", SevenZipMethodMapper.Map([0x04, 0x01, 0x08]));
    }

    [Fact]
    public void MethodMapper_BZip2_ReturnsBZip2()
    {
        Assert.Equal("BZip2", SevenZipMethodMapper.Map([0x04, 0x02, 0x02]));
    }

    [Fact]
    public void MethodMapper_PPMd_ReturnsPPMD()
    {
        Assert.Equal("PPMD", SevenZipMethodMapper.Map([0x03, 0x04, 0x01]));
    }

    [Fact]
    public void MethodMapper_UnknownId_NotEncryption()
    {
        Assert.False(SevenZipMethodMapper.IsEncryption([0xAB, 0xCD]));
    }

    // =================================================================
    //  头部解析器测试 (构造字节序列模拟 NextHeader)
    // =================================================================

    [Fact]
    public void ParseHeader_EmptyData_ReturnsEmptyResult()
    {
        var parser = new SevenZipHeaderParser();
        var result = parser.Parse([]);
        Assert.Equal(0, result.NumFiles);
        Assert.Empty(result.Files);
        Assert.False(result.HeaderIsCompressed);
    }

    [Fact]
    public void ParseHeader_OnlyEnd_ReturnsEmptyResult()
    {
        // kEnd (0x00)
        byte[] data = [0x00];
        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(data);
        Assert.Equal(0, result.NumFiles);
    }

    [Fact]
    public void ParseHeader_PlainHeader_WithOneFile_ReturnsFiles()
    {
        // 构建一个最简单的 Plain Header：
        // kHeader (0x01)
        //   kMainStreamsInfo (0x04)
        //     kPackInfo (0x06): PackPos=0, NumPackStreams=1, kSize, [100], kEnd
        //     kUnPackInfo (0x07): kFolder(0x0B), NumFolders=1, External=0
        //       NumCoders=1, flags=0x03|0x00=3(codecIdSize=3, simple), LZMA_ID(0x03,0x01,0x01)
        //       No bind pairs (numOut=1, numBindPairs=0)
        //       kCodersUnPackSize(0x0C): UnpackSize=1000
        //       kEnd
        //     kEnd
        //   kFilesInfo (0x05): NumFiles=1
        //     kName (0x11): External=0, "test.txt\0" (UTF-16LE)
        //     kEnd
        //   kEnd (0x00)
        // kEnd (0x00)

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader

        // MainStreamsInfo
        w.Write((byte)0x04); // kMainStreamsInfo

        // PackInfo
        w.Write((byte)0x06); // kPackInfo
        WriteVint(w, 0); // PackPos = 0
        WriteVint(w, 1); // NumPackStreams = 1
        w.Write((byte)0x09); // kSize
        WriteVint(w, 100); // PackSizes[0] = 100
        w.Write((byte)0x00); // kEnd of PackInfo

        // UnPackInfo (CodersInfo)
        w.Write((byte)0x07); // kUnPackInfo
        w.Write((byte)0x0B); // kFolder
        WriteVint(w, 1); // NumFolders = 1
        w.Write((byte)0x00); // External = 0 (inline)
        // Folder 1
        WriteVint(w, 1); // NumCoders = 1
        w.Write((byte)0x03); // flags: codecIdSize=3, simple
        w.Write((byte)0x03); w.Write((byte)0x01); w.Write((byte)0x01); // LZMA ID
        // NumBindPairs = 0 (1 outstream - 1 = 0)
        // NumPackedStreams = 1 (1 instream - 0 = 1), and it's 1 so no index
        w.Write((byte)0x0C); // kCodersUnPackSize
        WriteVint(w, 1000); // unpack size
        w.Write((byte)0x00); // kEnd of UnPackInfo

        w.Write((byte)0x00); // kEnd of MainStreamsInfo

        // FilesInfo
        w.Write((byte)0x05); // kFilesInfo
        WriteVint(w, 1); // NumFiles = 1

        // kName property
        w.Write((byte)0x11); // kName
        byte[] nameBytes = Encoding.Unicode.GetBytes("test.txt\0");
        WriteVint(w, 1 + nameBytes.Length); // Size = external (1) + name data
        w.Write((byte)0x00); // External = 0 (inline)
        w.Write(nameBytes); // "test.txt\0" in UTF-16LE

        w.Write((byte)0x00); // kEnd of FilesInfo
        w.Write((byte)0x00); // kEnd of Header

        byte[] data = ms.ToArray();
        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(data);

        Assert.False(result.HeaderIsCompressed);
        Assert.Equal(1, result.NumFiles);
        Assert.Single(result.Files);
        Assert.Equal("test.txt", result.Files[0].Name);
        Assert.False(result.IsEncrypted);
        Assert.Equal("LZMA", result.CompressionMethods);
    }

    [Fact]
    public void ParseHeader_WithMultipleNames_ReturnsAllFiles()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader
        w.Write((byte)0x05); // kFilesInfo
        WriteVint(w, 3); // NumFiles = 3

        // kName: 三个 UTF-16LE null-terminated 文件名
        w.Write((byte)0x11); // kName
        var names = new[] { "a.txt", "b.txt", "c.txt" };
        byte[] nameData;
        using (var nms = new MemoryStream())
        using (var nw = new BinaryWriter(nms))
        {
            nw.Write((byte)0x00); // External = 0
            foreach (var n in names)
                nw.Write(Encoding.Unicode.GetBytes(n + "\0"));
            nameData = nms.ToArray();
        }
        WriteVint(w, nameData.Length);
        w.Write(nameData);

        w.Write((byte)0x00); // kEnd of FilesInfo
        w.Write((byte)0x00); // kEnd of Header

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.Equal(3, result.NumFiles);
        Assert.Equal(3, result.Files.Count);
        Assert.Equal("a.txt", result.Files[0].Name);
        Assert.Equal("b.txt", result.Files[1].Name);
        Assert.Equal("c.txt", result.Files[2].Name);
    }

    [Fact]
    public void ParseHeader_EncodedHeader_DetectsCompressed()
    {
        // kEncodedHeader (0x17) → StreamsInfo
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x17); // kEncodedHeader

        // StreamsInfo with PackInfo
        w.Write((byte)0x06); // kPackInfo
        WriteVint(w, 0); // PackPos
        WriteVint(w, 2); // NumPackStreams = 2
        w.Write((byte)0x09); // kSize
        WriteVint(w, 500); // PackSizes[0]
        WriteVint(w, 300); // PackSizes[1]
        w.Write((byte)0x00); // kEnd of PackInfo

        w.Write((byte)0x00); // kEnd of StreamsInfo

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.True(result.HeaderIsCompressed);
        Assert.Equal(2, result.PackStreams.Count);
        Assert.Equal(500, result.PackStreams[0].PackSize);
        Assert.Equal(300, result.PackStreams[1].PackSize);
    }

    [Fact]
    public void ParseHeader_With7zAesCoder_DetectsEncryption()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader
        w.Write((byte)0x04); // kMainStreamsInfo
        w.Write((byte)0x06); // kPackInfo
        WriteVint(w, 0); // PackPos
        WriteVint(w, 1); // NumPackStreams = 1
        w.Write((byte)0x09); // kSize
        WriteVint(w, 200);
        w.Write((byte)0x00); // kEnd of PackInfo

        w.Write((byte)0x07); // kUnPackInfo
        w.Write((byte)0x0B); // kFolder
        WriteVint(w, 1); // NumFolders = 1
        w.Write((byte)0x00); // External = 0
        // Folder with 7zAES
        WriteVint(w, 1); // NumCoders = 1
        w.Write((byte)0x04); // flags: codecIdSize=4
        w.Write((byte)0x06); w.Write((byte)0xF1); w.Write((byte)0x07); w.Write((byte)0x01); // 7zAES
        w.Write((byte)0x0C); // kCodersUnPackSize
        WriteVint(w, 1000);
        w.Write((byte)0x00); // kEnd of UnPackInfo
        w.Write((byte)0x00); // kEnd of MainStreamsInfo
        w.Write((byte)0x05); // kFilesInfo
        WriteVint(w, 1); // NumFiles = 1
        w.Write((byte)0x00); // kEnd of FilesInfo
        w.Write((byte)0x00); // kEnd of Header

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.True(result.IsEncrypted);
        Assert.Contains("7zAES", result.CompressionMethods);
    }

    [Fact]
    public void ParseHeader_WithEmptyStream_MarksFileAsEmpty()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader
        w.Write((byte)0x05); // kFilesInfo
        WriteVint(w, 2); // NumFiles = 2

        // kEmptyStream: 文件 0 是目录（MSB-first: bit7→file0）
        w.Write((byte)0x0E); // kEmptyStream
        WriteVint(w, 1); // Size = 1 byte (2 bits)
        w.Write((byte)0x80); // MSB (bit7) = 1 → file 0 is empty stream

        // kName
        w.Write((byte)0x11); // kName
        byte[] nameData;
        using (var nms = new MemoryStream())
        using (var nw = new BinaryWriter(nms))
        {
            nw.Write((byte)0x00); // External = 0
            nw.Write(Encoding.Unicode.GetBytes("dir/\0"));
            nw.Write(Encoding.Unicode.GetBytes("file.txt\0"));
            nameData = nms.ToArray();
        }
        WriteVint(w, nameData.Length);
        w.Write(nameData);

        w.Write((byte)0x00); // kEnd of FilesInfo
        w.Write((byte)0x00); // kEnd of Header

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.Equal(2, result.NumFiles);
        Assert.True(result.Files[0].IsEmptyStream);
        Assert.False(result.Files[1].IsEmptyStream);
        Assert.Equal("dir/", result.Files[0].Name);
        Assert.Equal("file.txt", result.Files[1].Name);
    }

    [Fact]
    public void ParseHeader_TruncatedData_DoesNotCrash()
    {
        // 只有部分 kHeader 标记，后面数据截断
        byte[] data = [0x01, 0x05];
        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(data);
        // 不会抛出异常，返回部分结果
        Assert.NotNull(result);
    }

    [Fact]
    public void ParseHeader_PackInfo_WithCRC_ParsesSuccessfully()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader
        w.Write((byte)0x04); // kMainStreamsInfo
        w.Write((byte)0x06); // kPackInfo
        WriteVint(w, 32); // PackPos = 32 (after signature header)
        WriteVint(w, 2); // NumPackStreams = 2
        w.Write((byte)0x09); // kSize
        WriteVint(w, 1000);
        WriteVint(w, 2000);
        w.Write((byte)0x0A); // kCRC
        w.Write((byte)0x01); // AllAreDefined = true
        w.Write(0x12345678); // CRC for stream 0
        w.Write(0x9ABCDEF0); // CRC for stream 1
        w.Write((byte)0x00); // kEnd of PackInfo
        w.Write((byte)0x00); // kEnd of MainStreamsInfo
        w.Write((byte)0x00); // kEnd of Header

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.Equal(2, result.PackStreams.Count);
        Assert.Equal(1000, result.PackStreams[0].PackSize);
        Assert.Equal(2000, result.PackStreams[1].PackSize);
    }

    [Fact]
    public void ParseHeader_WithSubStreamsInfo_FindsFilesInfo()
    {
        // 模拟真实 7z 解压头的结构: kHeader → MainStreamsInfo(含 SubStreamsInfo) → FilesInfo
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((byte)0x01); // kHeader
        w.Write((byte)0x04); // kMainStreamsInfo

        // PackInfo (minimal)
        w.Write((byte)0x06); // kPackInfo
        WriteVint(w, 0); // PackPos
        WriteVint(w, 1); // NumPackStreams
        w.Write((byte)0x09); // kSize
        WriteVint(w, 100);
        w.Write((byte)0x00); // kEnd PackInfo

        // CodersInfo (minimal)
        w.Write((byte)0x07); // kUnPackInfo
        w.Write((byte)0x0B); // kFolder
        WriteVint(w, 1); // NumFolders
        w.Write((byte)0x00); // External
        WriteVint(w, 1); // NumCoders
        w.Write((byte)0x01); // flags: codecIdSize=1, simple, no attrs
        w.Write((byte)0x21); // LZMA2
        w.Write((byte)0x0C); // kCodersUnPackSize
        WriteVint(w, 200);
        w.Write((byte)0x00); // kEnd UnPackInfo

        // SubStreamsInfo: SubStreamsInfo 属性无 Size 字段，数据直接跟在 NID 后
        w.Write((byte)0x08); // kSubStreamsInfo
        w.Write((byte)0x0D); // kNumUnPackStream (无 Size)
        WriteVint(w, 1); // NumUnPackStreamsInFolders[0] = 1
        w.Write((byte)0x00); // kEnd SubStreamsInfo

        w.Write((byte)0x00); // kEnd MainStreamsInfo

        // FilesInfo
        w.Write((byte)0x05); // kFilesInfo
        WriteVint(w, 1); // NumFiles = 1
        w.Write((byte)0x11); // kName
        byte[] nb = Encoding.Unicode.GetBytes("file.txt\0");
        WriteVint(w, 1 + nb.Length); // Size = External(1) + names
        w.Write((byte)0x00); // External = 0
        w.Write(nb);
        w.Write((byte)0x00); // kEnd FilesInfo
        w.Write((byte)0x00); // kEnd Header

        var parser = new SevenZipHeaderParser();
        var result = parser.Parse(ms.ToArray());

        Assert.Equal(1, result.NumFiles);
        Assert.Single(result.Files);
        Assert.Equal("file.txt", result.Files[0].Name);
    }

    // =================================================================
    //  辅助方法：写入变长整数
    // =================================================================

    /// <summary>将 ulong 按 7z VINT 编码写入 BinaryWriter（额外字节小端序）</summary>
    private static void WriteVint(BinaryWriter w, ulong value)
    {
        if (value < 0x80UL)
        {
            w.Write((byte)value);
            return;
        }

        // 计算需要多少额外字节
        int bits = 0;
        ulong tmp = value;
        while (tmp > 0) { bits++; tmp >>= 1; }
        int extraBytes = (bits + 6) / 7;

        // extraBytes 范围 1..8
        byte first = (byte)((0xFF << (8 - extraBytes)) & 0xFF);
        ulong mask = (1UL << (7 - extraBytes)) - 1;
        first |= (byte)((value >> (extraBytes * 8)) & mask);
        w.Write(first);

        // 额外字节：小端序（低位在前）
        for (int i = 0; i < extraBytes; i++)
        {
            w.Write((byte)((value >> (i * 8)) & 0xFF));
        }
    }

    /// <summary>int 重载（自动转 ulong）</summary>
    private static void WriteVint(BinaryWriter w, int value) => WriteVint(w, (ulong)value);
}
