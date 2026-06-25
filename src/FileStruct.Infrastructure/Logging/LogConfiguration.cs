using FileStruct.Core.Models;
using Serilog;
using Serilog.Core;

namespace FileStruct.Infrastructure.Logging;

/// <summary>
/// Serilog 日志配置，根据 AppConfig.Debug 设置初始化
/// </summary>
public static class LogConfiguration
{
    /// <summary>
    /// 根据配置创建 Serilog Logger
    /// </summary>
    public static Logger CreateLogger(AppConfig config)
    {
        var level = config.Debug.Enabled
            ? Serilog.Events.LogEventLevel.Debug
            : Serilog.Events.LogEventLevel.Information;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(
                config.Debug.LogFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(1, config.Debug.LogFileRetentionDays),
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        // DEBUG 模式下额外输出到 Debug Output 窗口
        if (config.Debug.Enabled)
        {
            loggerConfig = loggerConfig.WriteTo.Debug(level);
        }

        return loggerConfig.CreateLogger();
    }
}
