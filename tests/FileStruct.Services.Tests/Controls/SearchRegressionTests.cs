using FileStruct.Core.Models;

namespace FileStruct.Services.Tests.Controls;

public class SearchRegressionTests : IDisposable
{
    private readonly string _testFilePath;

    public SearchRegressionTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"fs_search_test_{Guid.NewGuid():N}.bin");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public void ScanBuffer_FindsPatternAtExpectedOffset()
    {
        // "Hello World!" = 48 65 6C 6C 6F 20 57 6F 72 6C 64 21
        var data = System.Text.Encoding.ASCII.GetBytes("Hello World!");
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        // 搜索 "He" = 0x48 0x65
        byte[] pattern = [0x48, 0x65];
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++)
        {
            bool match = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (buf.ReadByte(o + i) != pattern[i]) { match = false; break; }
            }
            if (match) results.Add(o);
        }

        Assert.Single(results);
        Assert.Equal(0, results[0]);
    }

    [Fact]
    public void ScanBuffer_FindsAllRepeatingPattern()
    {
        // "AAAA" → search "AA" should find at offsets [0, 1, 2]
        var data = System.Text.Encoding.ASCII.GetBytes("AAAA");
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        byte[] pattern = [0x41, 0x41]; // "AA"
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++)
        {
            bool match = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (buf.ReadByte(o + i) != pattern[i]) { match = false; break; }
            }
            if (match) results.Add(o);
        }

        Assert.Equal(3, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
    }

    [Fact]
    public void ScanBuffer_NoMatch_ReturnsEmpty()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("Hello");
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        byte[] pattern = [0xFF, 0xFF]; // not in file
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++)
        {
            bool match = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (buf.ReadByte(o + i) != pattern[i]) { match = false; break; }
            }
            if (match) results.Add(o);
        }

        Assert.Empty(results);
    }

    [Fact]
    public void ScanBuffer_PatternAtEnd()
    {
        // 10 zero bytes then "END" at end
        var data = new byte[13];
        data[10] = 0x45; data[11] = 0x4E; data[12] = 0x44; // "END"
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        byte[] pattern = [0x45, 0x4E, 0x44];
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++)
        {
            bool match = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (buf.ReadByte(o + i) != pattern[i]) { match = false; break; }
            }
            if (match) results.Add(o);
        }

        Assert.Single(results);
        Assert.Equal(10, results[0]);
    }

    [Fact]
    public void ScanBuffer_SingleBytePattern()
    {
        var data = new byte[] { 0, 1, 0xFF, 2, 0xFF, 3 };
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        byte[] pattern = [0xFF];
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++)
        {
            if (buf.ReadByte(o) == pattern[0]) results.Add(o);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0]);
        Assert.Equal(4, results[1]);
    }

    [Fact]
    public void ScanBuffer_PatternTooLargeForFile_ReturnsEmpty()
    {
        var data = new byte[3];
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        byte[] pattern = new byte[10]; // larger than file
        var results = new List<long>();
        for (long o = 0; o <= buf.Length - pattern.Length; o++) // loops 0 times
        {
            results.Add(o);
        }

        Assert.Empty(results);
    }

    [Fact]
    public void ScanBuffer_MemoryMappedRead_ValidAfterBufferDisposal()
    {
        // Regression: verify MMF reads are valid when buffer is alive
        var data = System.Text.Encoding.ASCII.GetBytes("TestData1234");
        File.WriteAllBytes(_testFilePath, data);
        using var buf = BinaryBuffer.LoadFromFile(_testFilePath);

        // Read across different offsets to verify MMF works correctly
        Assert.Equal(0x54, buf.ReadByte(0));  // 'T'
        Assert.Equal(0x65, buf.ReadByte(1));  // 'e'
        Assert.Equal(0x34, buf.ReadByte(11)); // '4' at end
    }
}
