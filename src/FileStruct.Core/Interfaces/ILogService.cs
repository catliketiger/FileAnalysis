namespace FileStruct.Core.Interfaces;

/// <summary>
/// 日志服务：封装 Serilog，提供 DEBUG 模式日志输出
/// 满足开发规范#5：先按 DEBUG 模式开发交付，关键信息输出到 log 文件
/// </summary>
public interface ILogService
{
    /// <summary>DEBUG 级别日志</summary>
    void Debug(string message);

    /// <summary>INFO 级别日志</summary>
    void Info(string message);

    /// <summary>WARNING 级别日志</summary>
    void Warn(string message);

    /// <summary>ERROR 级别日志</summary>
    void Error(string message, Exception? exception = null);

    /// <summary>
    /// 性能追踪：返回一个 disposable，在 Dispose 时输出耗时
    /// </summary>
    IDisposable BeginOperation(string operationName);
}
