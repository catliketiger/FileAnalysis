using FileStruct.Core.Models;
using FileStruct.Infrastructure.Configuration;
using FileStruct.Services.Configuration;

namespace FileStruct.Services.Tests.Configuration;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _settingsPath;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _settingsPath = Path.Combine(_testDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private ConfigService CreateService()
    {
        var store = new ConfigFileStore(_settingsPath);
        return new ConfigService(store);
    }

    [Fact]
    public void GetConfig_ReturnsCurrentConfig()
    {
        var service = CreateService();
        var config = service.GetConfig();

        Assert.NotNull(config);
        Assert.Equal("Light", config.UI.Theme);
        Assert.Equal("#1976D2", config.UI.AccentColor);
    }

    [Fact]
    public void UpdateConfig_PersistsAndFiresEvent()
    {
        var service = CreateService();
        var fired = false;
        AppConfig? receivedConfig = null;

        service.ConfigChanged += (cfg) =>
        {
            fired = true;
            receivedConfig = cfg;
        };

        var newConfig = new AppConfig
        {
            UI = new UiConfig { Theme = "Dark", AccentColor = "#FF5722" }
        };

        service.UpdateConfig(newConfig);

        Assert.True(fired);
        Assert.NotNull(receivedConfig);
        Assert.Equal("Dark", receivedConfig!.UI.Theme);
        Assert.Equal("#FF5722", receivedConfig.UI.AccentColor);
        Assert.Equal("Dark", service.GetConfig().UI.Theme);
    }

    [Fact]
    public void ResetToDefault_FiresEvent()
    {
        var service = CreateService();

        service.UpdateConfig(new AppConfig
        {
            UI = new UiConfig { Theme = "Dark" }
        });

        var fired = false;
        service.ConfigChanged += (cfg) => fired = true;
        service.ResetToDefault();

        Assert.True(fired);
        Assert.Equal("Light", service.GetConfig().UI.Theme);
    }

    [Fact]
    public void GetConfig_NewFields_HaveDefaults()
    {
        var service = CreateService();
        var config = service.GetConfig();

        Assert.Equal("Auto", config.FileDefaults.DefaultDecodeType);
        Assert.Equal("#1976D2", config.UI.AccentColor);
        Assert.Equal(-1.0, config.UI.WindowLeft);
        Assert.Equal(-1.0, config.UI.WindowTop);
        Assert.Equal("Normal", config.UI.WindowState);
        Assert.Equal("", config.UI.LayoutState);
    }

    [Fact]
    public void ConfigChange_DoesNotFire_WhenNoSubscriber()
    {
        // Should not throw when ConfigChanged is null
        var service = CreateService();
        service.UpdateConfig(new AppConfig());
        service.ResetToDefault();
    }
}
