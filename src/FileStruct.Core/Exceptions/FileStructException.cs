using System.Collections.ObjectModel;

namespace FileStruct.Core.Exceptions;

/// <summary>
/// 所有 FileStruct 异常的基类
/// </summary>
public abstract class FileStructException : Exception
{
    protected FileStructException(string message) : base(message) { }
    protected FileStructException(string message, Exception inner) : base(message, inner) { }

    /// <summary>用户可操作的提示建议（如 "请检查文件路径是否有访问权限"）</summary>
    public string? UserAction { get; init; }

    /// <summary>上下文信息字典（用于日志记录）</summary>
    public Dictionary<string, object> ContextInfo { get; init; } = new();
}
