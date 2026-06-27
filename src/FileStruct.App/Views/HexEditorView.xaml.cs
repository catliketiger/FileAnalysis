using System.Windows.Controls;
using FileStruct.App.ViewModels;
using HexSelectionArgs = FileStruct.App.Controls.SelectionChangedEventArgs;

namespace FileStruct.App.Views;

public partial class HexEditorView : UserControl
{
    public HexEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => SubscribeEvents();
    }

    private bool _subscribed;

    private void SubscribeEvents()
    {
        if (_subscribed) return;
        _subscribed = true;

        // 右键菜单"添加书签"
        HexViewControl.BookmarkRequested += OnBookmarkRequested;

        // 右键菜单"创建字段"
        HexViewControl.CreateFieldRequested += OnCreateFieldRequested;

        // HexView 选择变更 → 实时预览 + 结构树高亮
        HexViewControl.Selection.SelectionChanged += OnHexSelectionChanged;
    }

    private void OnCreateFieldRequested(long offset, long length)
    {
        if (DataContext is MainViewModel mainVm)
            mainVm.CreateFieldFromSelection(offset, length);
    }

    private void OnBookmarkRequested(long offset)
    {
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.BookmarkList.AddBookmark($"偏移 0x{offset:X}", offset);
            mainVm.StatusText = $"已添加书签 @ 0x{offset:X}";
        }
    }

    private void OnHexSelectionChanged(object? sender, HexSelectionArgs args)
    {
        if (DataContext is not MainViewModel mainVm) return;
        var buffer = HexViewControl.Buffer;
        if (buffer == null || args.Length <= 0) return;

        mainVm.LivePreview.UpdateFromBuffer(buffer, args.StartOffset,
            (int)args.Length, mainVm.HexEditor.IsLittleEndian);
        mainVm.StructureTree.SelectNodeByOffset(args.StartOffset);
        mainVm.HexEditor.SelectionStart = args.StartOffset;
        mainVm.HexEditor.SelectionLength = args.Length;
        mainVm.HexEditor.SelectionInfo = $"选中: 偏移 0x{args.StartOffset:X}, 长度 {args.Length} 字节";
    }

    private void GroupSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var groupSize))
        {
            // 直接设 HexView DP（绕过绑定异步问题）
            HexViewControl.ByteGroupSize = groupSize;
            // 也同步 ViewModel 属性
            if (DataContext is MainViewModel mainVm)
                mainVm.HexEditor.ByteGroupSize = groupSize;
        }
    }
}
