using System.Text.Json;
using FileStruct.Core.Models;

namespace FileStruct.Core.Tests.Models;

public class AppConfigTests
{
    [Fact]
    public void Constructor_DefaultValues_AreCorrect()
    {
        var config = new AppConfig();

        // Debug defaults
        Assert.True(config.Debug.Enabled);
        Assert.Equal("Debug", config.Debug.LogLevel);

        // FileDefaults defaults
        Assert.Equal(10L * 1024 * 1024 * 1024, config.FileDefaults.MaxFileSize);
        Assert.Equal("UTF-8", config.FileDefaults.DefaultEncoding);
        Assert.Equal(2, config.FileDefaults.DefaultByteGroupSize);
        Assert.Equal("LittleEndian", config.FileDefaults.DefaultEndianness);
        Assert.Equal("Auto", config.FileDefaults.DefaultDecodeType);

        // UiConfig defaults
        Assert.Equal("Light", config.UI.Theme);
        Assert.Equal(1400.0, config.UI.WindowWidth);
        Assert.Equal(900.0, config.UI.WindowHeight);
        Assert.Equal("Cascadia Code", config.UI.FontFamily);
        Assert.Equal(13, config.UI.FontSize);
        Assert.Equal("#1976D2", config.UI.AccentColor);
        Assert.Equal(-1.0, config.UI.WindowLeft);
        Assert.Equal(-1.0, config.UI.WindowTop);
        Assert.Equal("Normal", config.UI.WindowState);
        Assert.Equal("", config.UI.LayoutState);

        // Recognition defaults
        Assert.Equal(10L * 1024 * 1024, config.Recognition.AsyncThreshold);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesNewFields()
    {
        var config = new AppConfig
        {
            UI = new UiConfig
            {
                Theme = "Dark",
                AccentColor = "#FF5722",
                WindowLeft = 100,
                WindowTop = 50,
                WindowState = "Maximized",
                LayoutState = @"{""LeftPanelWidth"":300,""RightPanelVisible"":true}",
                FontFamily = "Consolas",
                FontSize = 14,
                WindowWidth = 1600,
                WindowHeight = 1000,
            },
            FileDefaults = new FileDefaultsConfig
            {
                DefaultDecodeType = "Hex",
                DefaultByteGroupSize = 4,
                DefaultEndianness = "BigEndian",
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Dark", deserialized!.UI.Theme);
        Assert.Equal("#FF5722", deserialized.UI.AccentColor);
        Assert.Equal(100, deserialized.UI.WindowLeft);
        Assert.Equal(50, deserialized.UI.WindowTop);
        Assert.Equal("Maximized", deserialized.UI.WindowState);
        Assert.Equal(@"{""LeftPanelWidth"":300,""RightPanelVisible"":true}", deserialized.UI.LayoutState);
        Assert.Equal("Consolas", deserialized.UI.FontFamily);
        Assert.Equal(14, deserialized.UI.FontSize);
        Assert.Equal(1600.0, deserialized.UI.WindowWidth);
        Assert.Equal(1000.0, deserialized.UI.WindowHeight);
        Assert.Equal("Hex", deserialized.FileDefaults.DefaultDecodeType);
        Assert.Equal(4, deserialized.FileDefaults.DefaultByteGroupSize);
        Assert.Equal("BigEndian", deserialized.FileDefaults.DefaultEndianness);
    }

    [Fact]
    public void Serialization_MissingFields_DefaultsGracefully()
    {
        // Simulate old config JSON without new fields
        var oldJson = @"{""UI"":{""Theme"":""Dark""},""FileDefaults"":{}}";
        var config = JsonSerializer.Deserialize<AppConfig>(oldJson);

        Assert.NotNull(config);
        Assert.Equal("Dark", config!.UI.Theme);
        // New fields should have defaults
        Assert.Equal("#1976D2", config.UI.AccentColor);
        Assert.Equal(-1.0, config.UI.WindowLeft);
        Assert.Equal(-1.0, config.UI.WindowTop);
        Assert.Equal("Normal", config.UI.WindowState);
        Assert.Equal("", config.UI.LayoutState);
        Assert.Equal("Auto", config.FileDefaults.DefaultDecodeType);
    }

    [Fact]
    public void WindowPosition_DefaultIsCentered()
    {
        var config = new AppConfig();
        Assert.Equal(-1.0, config.UI.WindowLeft);
        Assert.Equal(-1.0, config.UI.WindowTop);
        // -1 means "not set, use default centering behavior"
    }

    [Fact]
    public void AccentColor_DefaultIsMaterialBlue()
    {
        var config = new AppConfig();
        Assert.Equal("#1976D2", config.UI.AccentColor);
    }
}
