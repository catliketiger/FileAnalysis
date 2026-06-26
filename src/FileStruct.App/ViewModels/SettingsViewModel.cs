using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.App.Services;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 设置窗口 ViewModel — 管理所有用户配置项
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _config;
    private readonly ThemeService _themeService;
    private AppConfig _original;

    public SettingsViewModel(IConfigService config, ThemeService themeService)
    {
        _config = config;
        _themeService = themeService;
        _original = Clone(config.GetConfig());
        LoadFrom(_original);
    }

    // ===== 通用设置 =====

    [ObservableProperty]
    private string _theme = "Light";

    [ObservableProperty]
    private string _accentColor = "#1976D2";

    [ObservableProperty]
    private string _fontFamily = "Cascadia Code";

    [ObservableProperty]
    private int _fontSize = 13;

    // ===== 显示设置 =====

    [ObservableProperty]
    private int _byteGroupSize = 2;

    [ObservableProperty]
    private string _endianness = "LittleEndian";

    [ObservableProperty]
    private string _defaultDecodeType = "Auto";

    // ===== 主题辅助属性 =====

    public bool IsDarkTheme
    {
        get => Theme != "Light";
        set => Theme = value ? "Dark" : "Light";
    }

    // ===== 命令 =====

    [RelayCommand]
    private void Apply()
    {
        var cfg = _config.GetConfig();
        ApplyTo(cfg);
        _config.UpdateConfig(cfg);
        _themeService.ApplyTheme(cfg.UI.Theme);
    }

    [RelayCommand]
    private void Ok()
    {
        Apply();
        CloseWindow();
    }

    [RelayCommand]
    private void Cancel()
    {
        // 恢复原始配置
        _config.UpdateConfig(_original);
        _themeService.ApplyTheme(_original.UI.Theme);
        CloseWindow();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        // 重置为出厂默认值
        var defaults = new AppConfig();
        LoadFrom(defaults);
    }

    // ===== 辅助方法 =====

    private void LoadFrom(AppConfig cfg)
    {
        Theme = cfg.UI.Theme;
        AccentColor = cfg.UI.AccentColor;
        FontFamily = cfg.UI.FontFamily;
        FontSize = cfg.UI.FontSize;
        ByteGroupSize = cfg.FileDefaults.DefaultByteGroupSize;
        Endianness = cfg.FileDefaults.DefaultEndianness;
        DefaultDecodeType = cfg.FileDefaults.DefaultDecodeType;
    }

    private void ApplyTo(AppConfig cfg)
    {
        cfg.UI.Theme = Theme;
        cfg.UI.AccentColor = AccentColor;
        cfg.UI.FontFamily = FontFamily;
        cfg.UI.FontSize = FontSize;
        cfg.FileDefaults.DefaultByteGroupSize = ByteGroupSize;
        cfg.FileDefaults.DefaultEndianness = Endianness;
        cfg.FileDefaults.DefaultDecodeType = DefaultDecodeType;
    }

    private static AppConfig Clone(AppConfig source)
    {
        return new AppConfig
        {
            Debug = new DebugConfig
            {
                Enabled = source.Debug.Enabled,
                LogLevel = source.Debug.LogLevel,
                LogFile = source.Debug.LogFile,
                LogFileRetentionDays = source.Debug.LogFileRetentionDays,
            },
            FileDefaults = new FileDefaultsConfig
            {
                MaxFileSize = source.FileDefaults.MaxFileSize,
                DefaultEncoding = source.FileDefaults.DefaultEncoding,
                DefaultByteGroupSize = source.FileDefaults.DefaultByteGroupSize,
                DefaultEndianness = source.FileDefaults.DefaultEndianness,
                DefaultDecodeType = source.FileDefaults.DefaultDecodeType,
            },
            UI = new UiConfig
            {
                Theme = source.UI.Theme,
                WindowWidth = source.UI.WindowWidth,
                WindowHeight = source.UI.WindowHeight,
                WindowLeft = source.UI.WindowLeft,
                WindowTop = source.UI.WindowTop,
                WindowState = source.UI.WindowState,
                FontFamily = source.UI.FontFamily,
                FontSize = source.UI.FontSize,
                AccentColor = source.UI.AccentColor,
                LayoutState = source.UI.LayoutState,
            },
            Recognition = new RecognitionConfig
            {
                AsyncThreshold = source.Recognition.AsyncThreshold,
                MaxHeuristicScanBytes = source.Recognition.MaxHeuristicScanBytes,
            },
        };
    }

    private static void CloseWindow()
    {
        foreach (Window win in Application.Current.Windows)
        {
            if (win.DataContext is SettingsViewModel)
            {
                win.Close();
                return;
            }
        }
    }
}
