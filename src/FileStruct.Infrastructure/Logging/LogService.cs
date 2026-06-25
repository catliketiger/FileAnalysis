using System.Diagnostics;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using Serilog;

namespace FileStruct.Infrastructure.Logging;

/// <summary>
/// 日志服务：封装 Serilog，提供 DEBUG 模式日志输出
/// 满足开发规范#5：先按 DEBUG 模式开发交付，关键信息输出到 log 文件
/// </summary>
public sealed class LogService : ILogService, IDisposable
{
    private readonly ILogger _logger;
    private bool _disposed;

    public LogService(AppConfig config)
    {
        _logger = LogConfiguration.CreateLogger(config);
        _logger.Debug("LogService 初始化完成 (Debug 模式: {DebugEnabled})", config.Debug.Enabled);
    }

    public void Debug(string message) => _logger.Debug("{Message}", message);
    public void Info(string message) => _logger.Information("{Message}", message);
    public void Warn(string message) => _logger.Warning("{Message}", message);

    public void Error(string message, Exception? exception = null)
    {
        if (exception != null)
            _logger.Error(exception, "{Message}", message);
        else
            _logger.Error("{Message}", message);
    }

    /// <summary>
    /// 性能追踪：返回一个 Disposable，在 Dispose 时输出耗时
    /// </summary>
    public IDisposable BeginOperation(string operationName)
    {
        return new OperationTracker(_logger, operationName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_logger as IDisposable)?.Dispose();
    }

    private sealed class OperationTracker : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationTracker(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
            _logger.Debug("→ {Operation} 开始", operationName);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Information("✓ {Operation} 完成, 耗时 {ElapsedMs}ms",
                _operationName, _stopwatch.ElapsedMilliseconds);
        }
    }
}
