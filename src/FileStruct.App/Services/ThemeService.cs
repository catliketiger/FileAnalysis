using System.Windows;

namespace FileStruct.App.Services;

/// <summary>
/// 主题服务：运行时切换 WPF ResourceDictionary 实现深色/浅色主题
/// </summary>
public class ThemeService
{
    private const string LightThemeUri = "Styles/Themes/LightTheme.xaml";
    private const string DarkThemeUri = "Styles/Themes/DarkTheme.xaml";

    /// <summary>当前主题名称（"Light" / "Dark"）</summary>
    public string CurrentTheme { get; private set; } = "Light";

    /// <summary>主题变更事件</summary>
    public event Action<string>? ThemeChanged;

    /// <summary>
    /// 应用指定主题
    /// </summary>
    public void ApplyTheme(string theme)
    {
        if (string.IsNullOrEmpty(theme))
            theme = "Light";

        theme = theme switch
        {
            "Dark" => "Dark",
            _ => "Light"
        };

        if (CurrentTheme == theme) return;

        var merged = Application.Current.Resources.MergedDictionaries;

        // 查找并替换主题字典
        ResourceDictionary? themeDict = null;
        foreach (var dict in merged)
        {
            var src = dict.Source?.OriginalString ?? "";
            if (src.EndsWith("LightTheme.xaml") || src.EndsWith("DarkTheme.xaml"))
            {
                themeDict = dict;
                break;
            }
        }

        var uri = new Uri(theme == "Dark" ? DarkThemeUri : LightThemeUri, UriKind.Relative);
        if (themeDict != null)
        {
            themeDict.Source = uri;
        }
        else
        {
            // 初次使用，插入到首位
            merged.Insert(0, new ResourceDictionary { Source = uri });
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>
    /// 在浅色和深色之间切换
    /// </summary>
    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "Light" ? "Dark" : "Light");
    }
}
