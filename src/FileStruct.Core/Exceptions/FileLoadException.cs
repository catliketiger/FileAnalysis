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
        UserAction = "请检查文件路径是否正确，以及是否有读取权限";
        ContextInfo["FilePath"] = filePath;
    }
}
