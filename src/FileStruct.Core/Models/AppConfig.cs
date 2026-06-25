namespace FileStruct.Core.Models;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppConfig
{
    /// <summary>DEBUG 模式配置</summary>
    public DebugConfig Debug { get; set; } = new();

    /// <summary>文件加载默认值</summary>
    public FileDefaultsConfig FileDefaults { get; set; } = new();

    /// <summary>UI 显示配置</summary>
    public UiConfig UI { get; set; } = new();

    /// <summary>结构识别配置</summary>
    public RecognitionConfig Recognition { get; set; } = new();
}

/// <summary>
/// DEBUG 模式配置（开发规范#5）
/// </summary>
public class DebugConfig
{
    /// <summary>是否启用 DEBUG 模式</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>日志级别</summary>
    public string LogLevel { get; set; } = "Debug";

    /// <summary>日志文件路径模板</summary>
    public string LogFile { get; set; } = "logs/file-struct-.log";

    /// <summary>日志文件保留天数</summary>
    public int LogFileRetentionDays { get; set; } = 7;
}

/// <summary>
/// 文件加载默认值
/// </summary>
public class FileDefaultsConfig
{
    /// <summary>最大文件大小（字节），默认 200MB</summary>
    public long MaxFileSize { get; set; } = 200 * 1024 * 1024;

    /// <summary>默认文本编码</summary>
    public string DefaultEncoding { get; set; } = "UTF-8";

    /// <summary>默认字节分组大小</summary>
    public int DefaultByteGroupSize { get; set; } = 2;

    /// <summary>默认字节序</summary>
    public string DefaultEndianness { get; set; } = "LittleEndian";
}

/// <summary>
/// UI 显示配置
/// </summary>
public class UiConfig
{
    /// <summary>主题 ("Light" / "Dark")</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>窗口默认宽度</summary>
    public double WindowWidth { get; set; } = 1400;

    /// <summary>窗口默认高度</summary>
    public double WindowHeight { get; set; } = 900;

    /// <summary>十六进制视图字体</summary>
    public string FontFamily { get; set; } = "Cascadia Code";

    /// <summary>十六进制视图字号</summary>
    public int FontSize { get; set; } = 13;
}

/// <summary>
/// 结构识别配置
/// </summary>
public class RecognitionConfig
{
    /// <summary>异步识别阈值（字节），超过此大小使用异步模式</summary>
    public long AsyncThreshold { get; set; } = 10 * 1024 * 1024;

    /// <summary>启发式引擎最大扫描字节数</summary>
    public long MaxHeuristicScanBytes { get; set; } = 64 * 1024 * 1024;
}
