using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using FileStruct.Services.ProjectManagement;
using System.IO;

namespace FileStruct.Integration.Tests;

/// <summary>
/// 项目保存/加载周期端到端集成测试
/// </summary>
public class ProjectSaveLoadCycleTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProjectSerializer _serializer;
    private readonly ILogService _logger;

    public ProjectSaveLoadCycleTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructIntegration", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _serializer = new ProjectSerializer();
        _logger = new TestLogger();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectWithBookmarks_DataPreserved()
    {
        // Arrange: 创建源文件
        var sourceFile = Path.Combine(_testDir, "source.bin");
        File.WriteAllBytes(sourceFile, new byte[1024]); // 1KB test file

        var project = new ProjectFile
        {
            SourceFile = new SourceFileInfo
            {
                OriginalPath = sourceFile,
                FileName = "source.bin",
                FileSize = 1024,
                Sha256Hash = await _serializer.ComputeHashAsync(sourceFile),
            },
            ViewState = new ViewState
            {
                ActiveView = "Hex",
                ScrollOffset = 128,
            },
            Bookmarks =
            [
                new Bookmark { Name = "Start", Offset = 0 },
                new Bookmark { Name = "End", Offset = 1020 },
            ],
        };

        var projectFile = Path.Combine(_testDir, "test.fstruct");
        var service = new ProjectService(_serializer, _logger);

        // Act: 保存项目
        await service.SaveAsync(project, projectFile);

        // Assert: 文件存在且非空
        Assert.True(File.Exists(projectFile));
        var fileInfo = new FileInfo(projectFile);
        Assert.True(fileInfo.Length > 0);

        // Act: 打开项目
        var loaded = await service.OpenAsync(projectFile);

        // Assert: 数据完整
        Assert.Equal(project.SourceFile.FileName, loaded.SourceFile.FileName);
        Assert.Equal(project.SourceFile.Sha256Hash, loaded.SourceFile.Sha256Hash);
        Assert.Equal(project.ViewState.ScrollOffset, loaded.ViewState.ScrollOffset);
        Assert.Equal(2, loaded.Bookmarks.Count);
        Assert.Equal("Start", loaded.Bookmarks[0].Name);
        Assert.Equal("End", loaded.Bookmarks[1].Name);
    }

    [Fact]
    public async Task HashVerification_ModifiedFile_ReturnsFalse()
    {
        // Arrange: 创建源文件和项目
        var sourceFile = Path.Combine(_testDir, "original.bin");
        File.WriteAllBytes(sourceFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var project = new ProjectFile
        {
            SourceFile = new SourceFileInfo
            {
                OriginalPath = sourceFile,
                FileName = "original.bin",
                FileSize = 4,
                Sha256Hash = await _serializer.ComputeHashAsync(sourceFile),
            },
        };

        var service = new ProjectService(_serializer, _logger);

        // Act: 修改源文件
        File.WriteAllBytes(sourceFile, new byte[] { 0xFF, 0xFE, 0xFD, 0xFC });

        // Assert: 哈希不匹配
        var match = service.VerifySourceFileHash(project, sourceFile);
        Assert.False(match);
    }

    [Fact]
    public async Task HashVerification_UnmodifiedFile_ReturnsTrue()
    {
        var sourceFile = Path.Combine(_testDir, "original.bin");
        File.WriteAllBytes(sourceFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var project = new ProjectFile
        {
            SourceFile = new SourceFileInfo
            {
                OriginalPath = sourceFile,
                Sha256Hash = await _serializer.ComputeHashAsync(sourceFile),
            },
        };

        var service = new ProjectService(_serializer, _logger);
        var match = service.VerifySourceFileHash(project, sourceFile);

        Assert.True(match);
    }
}

/// <summary>
/// 测试用日志（输出到控制台）
/// </summary>
internal class TestLogger : ILogService
{
    public void Debug(string message) => System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
    public void Info(string message) => System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
    public void Warn(string message) => System.Diagnostics.Debug.WriteLine($"[WARN] {message}");
    public void Error(string message, Exception? exception = null) =>
        System.Diagnostics.Debug.WriteLine($"[ERROR] {message} {exception}");

    public IDisposable BeginOperation(string operationName)
    {
        Info($"→ {operationName} 开始");
        return new DisposableAction(() => Info($"✓ {operationName} 完成"));
    }
}

internal class DisposableAction : IDisposable
{
    private readonly Action _action;
    public DisposableAction(Action action) => _action = action;
    public void Dispose() => _action();
}
