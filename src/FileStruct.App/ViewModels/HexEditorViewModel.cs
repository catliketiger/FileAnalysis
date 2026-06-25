using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

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
    private long _selectionStart = -1;

    [ObservableProperty]
    private long _selectionLength;

    [ObservableProperty]
    private long _totalBytes;

    partial void OnBufferChanged(BinaryBuffer? value)
    {
        TotalBytes = value?.Length ?? 0;
        ScrollOffset = 0;
        SelectionInfo = "";
        SelectionStart = -1;
        SelectionLength = 0;
    }
}
