namespace FileStruct.Core.Exceptions;

/// <summary>
/// 文件损坏：文件头不完整、截断、校验和不匹配等
/// </summary>
public class FileCorruptedException : FileStructException
{
    public string FilePath { get; }

    public FileCorruptedException(string message, string filePath, Exception? inner = null)
        : base(message, inner!)
    {
        FilePath = filePath;
    }
}
