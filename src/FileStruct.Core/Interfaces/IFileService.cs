using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

/// <summary>
/// 文件管理服务：负责文件加载、类型检测、生命周期管理
/// </summary>
public interface IFileService
{
    /// <summary>
    /// 异步加载文件，返回 BinaryBuffer
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>BinaryBuffer 实例</returns>
    Task<BinaryBuffer> LoadFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检测文件类型
    /// </summary>
    FileTypeInfo DetectFileType(string filePath);

    /// <summary>
    /// 检测文件类型（基于已加载的二进制数据，用于无扩展名文件）
    /// </summary>
    FileTypeInfo DetectFileType(BinaryBuffer buffer);

    /// <summary>
    /// 释放文件缓冲
    /// </summary>
    void CloseFile(BinaryBuffer buffer);
}
