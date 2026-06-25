using FileStruct.Core.Models;

namespace FileStruct.Core.Exceptions;

/// <summary>
/// 文件大小超过上限（默认 200MB）
/// </summary>
public class FileTooLargeException : FileStructException
{
    public long FileSize { get; }
    public long MaxSize { get; }

    public FileTooLargeException(long fileSize, long maxSize = BinaryBuffer.DefaultMaxFileSize)
        : base($"文件大小 ({fileSize:N0} 字节) 超过上限 ({maxSize:N0} 字节)")
    {
        FileSize = fileSize;
        MaxSize = maxSize;
    }
}
