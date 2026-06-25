using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 十六进制编辑器视图模型
/// </summary>
public partial class HexEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private BinaryBuffer? _buffer;

    [ObservableProperty]
    private FileTypeInfo? _fileType;

    [ObservableProperty]
    private long _scrollOffset;

    [ObservableProperty]
    private int _byteGroupSize = 2;

    [ObservableProperty]
    private bool _isLittleEndian = true;

    [ObservableProperty]
    private string _selectionInfo = "";

    [ObservableProperty]
    private long _totalBytes;

    partial void OnBufferChanged(BinaryBuffer? value)
    {
        TotalBytes = value?.Length ?? 0;
        ScrollOffset = 0;
        SelectionInfo = "";
    }
}
