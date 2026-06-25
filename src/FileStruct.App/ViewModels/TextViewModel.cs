using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

public partial class TextViewModel : ObservableObject
{
    private BinaryBuffer? _buffer;

    [ObservableProperty]
    private string _textContent = "";

    [ObservableProperty]
    private string _encodingName = "UTF-8";

    [ObservableProperty]
    private bool _hasContent;

    public void LoadText(BinaryBuffer buffer, Encoding encoding)
    {
        _buffer = buffer;
        ReloadText(encoding);
    }

    public void ReloadText(Encoding encoding)
    {
        if (_buffer == null) return;
        var maxRead = (int)Math.Min(_buffer.Length, 1024 * 1024);
        var bytes = _buffer.ReadBytes(0, maxRead);
        TextContent = encoding.GetString(bytes);
        EncodingName = encoding.WebName;
        HasContent = true;
    }

    public void ReloadByEncodingName(string encodingName)
    {
        try
        {
            var encoding = Encoding.GetEncoding(encodingName);
            ReloadText(encoding);
        }
        catch
        {
            // 编码名称无效时不处理
        }
    }

    public void Clear()
    {
        _buffer = null;
        TextContent = "";
        HasContent = false;
    }
}
