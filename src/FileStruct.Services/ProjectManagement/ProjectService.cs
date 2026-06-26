using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.ProjectManagement;

/// <summary>
/// 项目管理服务：保存/打开项目工程文件，包含哈希校验
/// </summary>
public class ProjectService : IProjectService
{
    private readonly ProjectSerializer _serializer;
    private readonly ILogService _logger;

    public ProjectService(ProjectSerializer serializer, ILogService logger)
    {
        _serializer = serializer;
        _logger = logger;
    }

    public async Task SaveAsync(ProjectFile project, string filePath,
        CancellationToken cancellationToken = default)
    {
        using var op = _logger.BeginOperation($"保存项目: {filePath}");
        _logger.Debug($"保存项目到: {filePath}");

        // 计算源文件哈希
        if (!string.IsNullOrEmpty(project.SourceFile.OriginalPath) &&
            File.Exists(project.SourceFile.OriginalPath))
        {
            project.SourceFile.Sha256Hash =
                await _serializer.ComputeHashAsync(project.SourceFile.OriginalPath, cancellationToken);
        }

        var json = _serializer.Serialize(project);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.Info($"项目已保存: {filePath}");
    }

    public async Task<ProjectFile> OpenAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        using var op = _logger.BeginOperation($"打开项目: {filePath}");
        _logger.Debug($"打开项目文件: {filePath}");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"项目文件未找到: {filePath}");

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var project = _serializer.Deserialize(json);

        _logger.Info($"项目已打开: {filePath}, 源文件: {project.SourceFile.FileName}");
        return project;
    }

    public bool VerifySourceFileHash(ProjectFile project, string sourceFilePath)
    {
        if (string.IsNullOrEmpty(project.SourceFile.Sha256Hash))
            return true; // 无哈希记录时跳过校验

        if (!File.Exists(sourceFilePath))
            return false;

        // 使用同步方式计算哈希，避免在 UI 线程上 .GetResult() 死锁
        using var stream = File.OpenRead(sourceFilePath);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(stream);
        var hash = Convert.ToHexStringLower(hashBytes);
        var match = string.Equals(hash, project.SourceFile.Sha256Hash,
            StringComparison.OrdinalIgnoreCase);

        if (!match)
        {
            _logger.Warn($"源文件哈希不匹配: {sourceFilePath} " +
                         $"(记录: {project.SourceFile.Sha256Hash}, 实际: {hash})");
        }

        return match;
    }

    public async Task<string> ComputeHashAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        return await _serializer.ComputeHashAsync(filePath, cancellationToken);
    }
}
