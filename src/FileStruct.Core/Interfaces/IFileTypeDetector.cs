using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

/// <summary>
/// 文件类型检测器：基于扩展名和魔数识别文件类型
/// </summary>
public interface IFileTypeDetector
{
    /// <summary>
    /// 基于文件路径（扩展名）进行基础类型判断
    /// </summary>
    FileTypeInfo DetectByExtension(string filePath);

    /// <summary>
    /// 基于文件头部字节（魔数）进行精准匹配
    /// </summary>
    FileTypeInfo DetectByHeader(byte[] headerBytes);

    /// <summary>
    /// 综合判断：先读头部魔数，再结合扩展名给出最终结果
    /// </summary>
    FileTypeInfo Detect(string filePath, byte[] headerBytes);
}
