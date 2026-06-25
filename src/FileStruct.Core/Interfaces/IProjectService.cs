using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

/// <summary>
/// 项目管理服务：保存/打开项目工程文件
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// 异步保存项目到文件
    /// </summary>
    /// <param name="project">项目数据</param>
    /// <param name="filePath">目标路径（.fstruct 文件）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(ProjectFile project, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步打开项目文件
    /// </summary>
    /// <param name="filePath">项目文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>项目数据</returns>
    Task<ProjectFile> OpenAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验源文件哈希是否与项目记录一致
    /// </summary>
    /// <returns>true 一致，false 不一致（文件已被修改）</returns>
    bool VerifySourceFileHash(ProjectFile project, string sourceFilePath);

    /// <summary>
    /// 计算文件的 SHA256 哈希
    /// </summary>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);
}
