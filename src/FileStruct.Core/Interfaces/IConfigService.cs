using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

/// <summary>
/// 配置服务：应用程序设置管理
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 获取当前配置
    /// </summary>
    AppConfig GetConfig();

    /// <summary>配置变更事件，更新后触发</summary>
    event Action<AppConfig>? ConfigChanged;

    /// <summary>
    /// 更新配置并持久化
    /// </summary>
    void UpdateConfig(AppConfig config);

    /// <summary>
    /// 重置配置为默认值
    /// </summary>
    void ResetToDefault();
}
