using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 纯文本视图模型
/// </summary>
public partial class TextViewModel : ObservableObject
{
    [ObservableProperty]
    private string _textContent = "";

    [ObservableProperty]
    private string _encodingName = "UTF-8";

    [ObservableProperty]
    private bool _hasContent;

    /// <summary>
    /// 从二进制缓冲区加载文本
    /// </summary>
    public void LoadText(BinaryBuffer buffer, Encoding encoding)
    {
        // 文件较大时只读取前 1MB 用于显示
        var maxRead = (int)Math.Min(buffer.Length, 1024 * 1024);
        var bytes = buffer.ReadBytes(0, maxRead);
        TextContent = encoding.GetString(bytes);
        EncodingName = encoding.WebName;
        HasContent = true;
    }

    /// <summary>
    /// 清除文本内容
    /// </summary>
    public void Clear()
    {
        TextContent = "";
        HasContent = false;
    }
}
