namespace FileStruct.Core.Exceptions;

/// <summary>
/// 文件加载失败：文件不存在、无权限、路径过长等
/// </summary>
public class FileLoadException : FileStructException
{
    public string FilePath { get; }

    public FileLoadException(string message, string filePath, Exception? inner = null)
        : base(message, inner!)
    {
        FilePath = filePath;
    }
}
