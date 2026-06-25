namespace FileStruct.Core.Exceptions;

/// <summary>
/// 所有 FileStruct 异常的基类
/// </summary>
public abstract class FileStructException : Exception
{
    protected FileStructException(string message) : base(message) { }
    protected FileStructException(string message, Exception inner) : base(message, inner) { }
}
