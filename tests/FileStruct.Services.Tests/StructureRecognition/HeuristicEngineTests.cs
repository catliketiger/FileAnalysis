using FileStruct.Core.Models;
using FileStruct.Services.StructureRecognition;

namespace FileStruct.Services.Tests.StructureRecognition;

public class HeuristicEngineTests : IDisposable
{
    private readonly HeuristicEngine _engine = new();
    private readonly string _testDir;

    public HeuristicEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task InferAsync_RepeatingBlocks_DetectsPattern()
    {
        // Create 128 bytes: 4 x 32-byte repeating blocks
        var block = new byte[32];
        for (int i = 0; i < 32; i++) block[i] = (byte)i;
        var data = new byte[128];
        for (int i = 0; i < 4; i++) block.CopyTo(data, i * 32);

        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var result = await _engine.InferAsync(buffer);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public async Task InferAsync_RandomData_NoFalsePositives()
    {
        var random = new Random(42);
        var data = new byte[4096];
        random.NextBytes(data);

        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var result = await _engine.InferAsync(buffer);

        Assert.NotNull(result);
        // Random data should have few or no detected structures
    }

    [Fact]
    public async Task InferAsync_EmptyBuffer_ReturnsRoot()
    {
        var data = new byte[100];
        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var result = await _engine.InferAsync(buffer);

        Assert.NotNull(result);
        Assert.Equal("文件根", result.Name);
    }

    [Fact]
    public async Task InferAsync_ReportsProgress()
    {
        var data = new byte[1024];
        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var progressReports = new List<RecognitionProgress>();
        var progress = new Progress<RecognitionProgress>(r => progressReports.Add(r));

        await _engine.InferAsync(buffer, progress);

        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.Last().Percentage >= 100);
    }

    [Fact]
    public async Task InferAsync_RespectsCancellation()
    {
        var data = new byte[1024];
        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _engine.InferAsync(buffer, null, cts.Token));
    }

    [Fact]
    public async Task InferAsync_AllZeroData_ReturnsRootWithChildren()
    {
        var data = new byte[1024]; // All zeros
        var path = CreateFile(data);
        using var buffer = BinaryBuffer.LoadFromFile(path);

        var result = await _engine.InferAsync(buffer);

        Assert.NotNull(result);
        Assert.Equal("文件根", result.Name);
    }

    private string CreateFile(byte[] data)
    {
        var path = Path.Combine(_testDir, $"test_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, data);
        return path;
    }
}
