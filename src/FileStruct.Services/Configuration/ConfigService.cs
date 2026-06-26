using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using FileStruct.Infrastructure.Configuration;

namespace FileStruct.Services.Configuration;

/// <summary>
/// 配置服务：应用程序设置管理
/// </summary>
public class ConfigService : IConfigService
{
    private readonly ConfigFileStore _store;
    private AppConfig _config;

    public ConfigService()
    {
        _store = new ConfigFileStore();
        _config = _store.LoadConfig();
    }

    /// <summary>
    /// 使用自定义存储路径（用于测试或 DI 注入）
    /// </summary>
    public ConfigService(ConfigFileStore store)
    {
        _store = store;
        _config = _store.LoadConfig();
    }

    public AppConfig GetConfig() => _config;

    /// <summary>配置变更事件，更新后触发</summary>
    public event Action<AppConfig>? ConfigChanged;

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _store.SaveConfig(config);
        ConfigChanged?.Invoke(config);
    }

    public void ResetToDefault()
    {
        _config = new AppConfig();
        _store.SaveConfig(_config);
        ConfigChanged?.Invoke(_config);
    }
}
