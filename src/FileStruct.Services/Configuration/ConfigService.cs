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

    public AppConfig GetConfig() => _config;

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _store.SaveConfig(config);
    }

    public void ResetToDefault()
    {
        _config = new AppConfig();
        _store.SaveConfig(_config);
    }
}
