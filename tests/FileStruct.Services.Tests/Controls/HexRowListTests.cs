using FileStruct.App.Controls;
using FileStruct.Core.Models;

namespace FileStruct.Services.Tests.Controls;

public class HexRowListTests : IDisposable
{
    private readonly string _testFilePath;

    public HexRowListTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"fs_hexrow_test_{Guid.NewGuid():N}.bin");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public void Count_CorrectlyRounded()
    {
        File.WriteAllBytes(_testFilePath, new byte[100]);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        // 100 bytes / 16 = 6.25 → 7 rows
        Assert.Equal(7, list.Count);
    }

    [Fact]
    public void Count_ExactMultiple()
    {
        File.WriteAllBytes(_testFilePath, new byte[64]);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        Assert.Equal(4, list.Count);
    }

    [Fact]
    public void Count_SingleByte()
    {
        File.WriteAllBytes(_testFilePath, new byte[1]);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        Assert.Equal(1, list.Count); // 1 byte / 16 = 0.0625 → 1 row
    }

    [Fact]
    public void Indexer_Row0_HasCorrectOffsets()
    {
        var data = new byte[32];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i + 1);
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        var row = (HexRowData?)list[0];
        Assert.NotNull(row);
        Assert.Equal(0, row!.RowOffset);
        Assert.Equal(16, row.RowByteCount);
        // 每个 ByteCell 的 Offset 是文件绝对偏移
        Assert.Equal(16, row.Bytes.Length);
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(i, row.Bytes[i].Offset);
        }
    }

    [Fact]
    public void Indexer_Row1_ByteCellOffsetsAreAbsolute()
    {
        var data = new byte[32];
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        var row = (HexRowData?)list[1];
        Assert.NotNull(row);
        Assert.Equal(16, row!.RowOffset);
        // ByteCell.Offset 是文件绝对偏移
        Assert.Equal(16, row.Bytes[0].Offset);
        Assert.Equal(31, row.Bytes[^1].Offset);
    }

    [Fact]
    public void Indexer_OutOfRange_ReturnsNull()
    {
        File.WriteAllBytes(_testFilePath, new byte[16]);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        Assert.Null(list[-1]);
        Assert.Null(list[10]);
    }

    [Fact]
    public void Indexer_LastRow_IsPartial()
    {
        File.WriteAllBytes(_testFilePath, new byte[20]);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);
        var list = new HexRowList(buf, 16, 2);

        var row = (HexRowData?)list[1]; // second row, only 4 bytes
        Assert.NotNull(row);
        Assert.Equal(4, row!.RowByteCount);
        Assert.Equal(4, row.Bytes.Length);
        Assert.False(row.IsCompleteRow);
    }
}
