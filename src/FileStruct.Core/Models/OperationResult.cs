namespace FileStruct.Core.Models;

/// <summary>
/// 操作结果封装 — 替代异常抛出模式，携带成功/失败状态和用户提示
/// </summary>
public class OperationResult<T>
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>结果数据（成功时有效）</summary>
    public T? Data { get; init; }

    /// <summary>错误消息（失败时有效）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>用户可操作的建议（如 "请检查文件路径"）</summary>
    public string? UserAction { get; init; }

    /// <summary>错误代码</summary>
    public string? ErrorCode { get; init; }

    /// <summary>创建成功结果</summary>
    public static OperationResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
    };

    /// <summary>创建失败结果</summary>
    public static OperationResult<T> Fail(string error, string? userAction = null, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = error,
        UserAction = userAction,
        ErrorCode = errorCode,
    };
}

/// <summary>
/// 无数据返回值的操作结果
/// </summary>
public class OperationResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误消息（失败时有效）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>用户可操作的建议</summary>
    public string? UserAction { get; init; }

    /// <summary>错误代码</summary>
    public string? ErrorCode { get; init; }

    public static OperationResult Ok() => new() { Success = true };

    public static OperationResult Fail(string error, string? userAction = null, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = error,
        UserAction = userAction,
        ErrorCode = errorCode,
    };
}
