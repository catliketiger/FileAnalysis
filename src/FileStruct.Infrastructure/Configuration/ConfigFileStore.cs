using System.Text.Json;
using FileStruct.Core.Models;

namespace FileStruct.Infrastructure.Configuration;

/// <summary>
/// 配置文件持久化存储：读取和写入 appsettings.json 及用户配置覆盖
/// </summary>
public class ConfigFileStore
{
    private readonly string _appSettingsPath;
    private readonly string _userSettingsPath;

    /// <summary>
    /// 使用默认路径初始化
    /// </summary>
    public ConfigFileStore()
    {
        _appSettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        _userSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileStruct", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_userSettingsPath)!);
    }

    /// <summary>
    /// 读取配置：合并内置配置和用户覆盖
    /// </summary>
    public AppConfig LoadConfig()
    {
        var config = new AppConfig();

        if (File.Exists(_appSettingsPath))
        {
            var json = File.ReadAllText(_appSettingsPath);
            var appConfig = JsonSerializer.Deserialize<AppConfig>(json);
            if (appConfig != null) config = appConfig;
        }

        if (File.Exists(_userSettingsPath))
        {
            var json = File.ReadAllText(_userSettingsPath);
            var userConfig = JsonSerializer.Deserialize<AppConfig>(json);
            if (userConfig != null) MergeConfig(config, userConfig);
        }

        return config;
    }

    /// <summary>
    /// 保存用户配置覆盖
    /// </summary>
    public void SaveConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_userSettingsPath, json);
    }

    /// <summary>
    /// 将 source 中的非默认值合并到 target
    /// </summary>
    private static void MergeConfig(AppConfig target, AppConfig source)
    {
        if (source.Debug.Enabled != new DebugConfig().Enabled)
            target.Debug.Enabled = source.Debug.Enabled;

        if (source.FileDefaults.MaxFileSize != new FileDefaultsConfig().MaxFileSize)
            target.FileDefaults.MaxFileSize = source.FileDefaults.MaxFileSize;
        if (source.FileDefaults.DefaultByteGroupSize != new FileDefaultsConfig().DefaultByteGroupSize)
            target.FileDefaults.DefaultByteGroupSize = source.FileDefaults.DefaultByteGroupSize;
        if (source.FileDefaults.DefaultEndianness != null)
            target.FileDefaults.DefaultEndianness = source.FileDefaults.DefaultEndianness;

        if (source.UI.Theme != new UiConfig().Theme)
            target.UI.Theme = source.UI.Theme;
        if (source.UI.WindowWidth != new UiConfig().WindowWidth)
            target.UI.WindowWidth = source.UI.WindowWidth;
        if (source.UI.WindowHeight != new UiConfig().WindowHeight)
            target.UI.WindowHeight = source.UI.WindowHeight;
        if (source.UI.FontFamily != null)
            target.UI.FontFamily = source.UI.FontFamily;
        if (source.UI.FontSize != new UiConfig().FontSize)
            target.UI.FontSize = source.UI.FontSize;
    }
}
