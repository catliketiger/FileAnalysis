using FileStruct.Core.Models;

namespace FileStruct.Core.Tests.Models;

public class BinaryBufferTests : IDisposable
{
    private readonly string _testDir;

    public BinaryBufferTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    #region LoadFromFile

    [Fact]
    public void LoadFromFile_ValidFile_ReturnsBuffer()
    {
        var path = CreateTestFile(100, 0xAB);

        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.Equal(100, buffer.Length);
        Assert.Equal(path, buffer.FilePath);
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public void LoadFromFile_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_testDir, "nonexistent.bin");

        Assert.Throws<System.IO.FileNotFoundException>(() =>
            BinaryBuffer.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_FileExceedsMaxSize_ThrowsException()
    {
        var path = CreateTestFile(100);

        Assert.Throws<FileStruct.Core.Exceptions.FileTooLargeException>(() =>
            BinaryBuffer.LoadFromFile(path, maxSize: 50));
    }

    [Fact]
    public void LoadFromFile_EmptyFile_ThrowsException()
    {
        var path = CreateTestFile(0);

        Assert.Throws<FileStruct.Core.Exceptions.FileLoadException>(() =>
            BinaryBuffer.LoadFromFile(path));
    }

    #endregion

    #region ReadByte

    [Fact]
    public void ReadByte_ValidOffset_ReturnsCorrectValue()
    {
        var path = CreateTestFile(10, 0x42);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var value = buffer.ReadByte(0);

        Assert.Equal(0x42, value);
    }

    [Fact]
    public void ReadByte_InvalidOffset_ThrowsArgumentOutOfRange()
    {
        var path = CreateTestFile(10);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.ReadByte(100));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.ReadByte(-1));
    }

    #endregion

    #region ReadUInt16

    [Theory]
    [InlineData(true)]  // Little-endian
    [InlineData(false)] // Big-endian
    public void ReadUInt16_ReadsCorrectValue(bool littleEndian)
    {
        var path = Path.Combine(_testDir, $"uint16_{littleEndian}.bin");
        File.WriteAllBytes(path, [0x01, 0x02, 0x03, 0x04]);

        using var buffer = BinaryBuffer.LoadFromFile(path);
        var value = buffer.ReadUInt16(0, littleEndian);
        var expected = littleEndian ? (ushort)0x0201 : (ushort)0x0102;

        Assert.Equal(expected, value);
    }

    #endregion

    #region ReadBytes

    [Fact]
    public void ReadBytes_ValidRange_ReturnsCorrectData()
    {
        var data = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var path = CreateTestFile(5);
        File.WriteAllBytes(path, data);

        using var buffer = BinaryBuffer.LoadFromFile(path);
        var result = buffer.ReadBytes(1, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(0x20, result[0]);
        Assert.Equal(0x30, result[1]);
        Assert.Equal(0x40, result[2]);
    }

    [Fact]
    public void ReadBytes_OutOfRange_ThrowsArgumentOutOfRange()
    {
        var path = CreateTestFile(10);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.ReadBytes(5, 10));
    }

    #endregion

    #region ReadString

    [Fact]
    public void ReadString_DifferentEncodings_ReturnsCorrectString()
    {
        var text = "Hello 世界";
        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var path = CreateTestFile(utf8Bytes.Length);
        File.WriteAllBytes(path, utf8Bytes);

        using var buffer = BinaryBuffer.LoadFromFile(path);
        var result = buffer.ReadString(0, utf8Bytes.Length, System.Text.Encoding.UTF8);

        Assert.Equal(text, result);
    }

    #endregion

    #region IsValidOffset / IsValidRange

    [Fact]
    public void IsValidOffset_ChecksBoundaries()
    {
        var path = CreateTestFile(100);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.True(buffer.IsValidOffset(0));
        Assert.True(buffer.IsValidOffset(99));
        Assert.False(buffer.IsValidOffset(100));
        Assert.False(buffer.IsValidOffset(-1));
    }

    [Fact]
    public void IsValidRange_ChecksBoundaries()
    {
        var path = CreateTestFile(100);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.True(buffer.IsValidRange(0, 100));
        Assert.True(buffer.IsValidRange(50, 50));
        Assert.False(buffer.IsValidRange(50, 51));
        Assert.False(buffer.IsValidRange(0, 0));
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var path = CreateTestFile(10);
        var buffer = BinaryBuffer.LoadFromFile(path);

        buffer.Dispose();
        buffer.Dispose(); // Should not throw

        Assert.True(buffer.IsDisposed);
    }

    [Fact]
    public void Dispose_AfterDispose_ReadThrows()
    {
        var path = CreateTestFile(10);
        var buffer = BinaryBuffer.LoadFromFile(path);
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.ReadByte(0));
    }

    #endregion

    #region 200MB Boundary

    [Fact]
    public void LoadFromFile_Exactly200MB_LoadsSuccessfully()
    {
        var path = CreateTestFile(200 * 1024 * 1024);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        Assert.Equal(200 * 1024 * 1024, buffer.Length);
    }

    #endregion

    #region Helpers

    private string CreateTestFile(long size, byte fillByte = 0x00)
    {
        var path = Path.Combine(_testDir, $"test_{Guid.NewGuid():N}.bin");
        using var stream = File.Create(path);
        if (size > 0)
        {
            var buffer = new byte[Math.Min(size, 1024 * 1024)];
            Array.Fill(buffer, fillByte);
            long written = 0;
            while (written < size)
            {
                var toWrite = (int)Math.Min(buffer.Length, size - written);
                stream.Write(buffer, 0, toWrite);
                written += toWrite;
            }
        }
        return path;
    }

    #endregion
}
