using System.Windows.Controls;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class HexEditorView : UserControl
{
    public HexEditorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 右键菜单"添加书签"
        HexViewControl.BookmarkRequested += offset =>
        {
            if (DataContext is MainViewModel mainVm)
            {
                mainVm.BookmarkList.AddBookmark($"偏移 0x{offset:X}", offset);
                mainVm.StatusText = $"已添加书签 @ 0x{offset:X}";
            }
        };

        // HexView 选择变更 → 实时预览 + 结构树高亮
        HexViewControl.Selection.SelectionChanged += (_, args) =>
        {
            if (DataContext is not MainViewModel mainVm) return;
            var buffer = HexViewControl.Buffer;
            if (buffer == null || args.Length <= 0) return;

            // 更新实时预览
            mainVm.LivePreview.UpdateFromBuffer(buffer, args.StartOffset,
                (int)args.Length, mainVm.HexEditor.IsLittleEndian);

            // 更新结构树高亮
            mainVm.StructureTree.SelectNodeByOffset(args.StartOffset);

            // 更新选择信息
            mainVm.HexEditor.SelectionStart = args.StartOffset;
            mainVm.HexEditor.SelectionLength = args.Length;
            mainVm.HexEditor.SelectionInfo = $"选中: 偏移 0x{args.StartOffset:X}, 长度 {args.Length} 字节";
        };
    }

    private void GroupSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var groupSize) &&
            DataContext is MainViewModel mainVm)
        {
            mainVm.HexEditor.ByteGroupSize = groupSize;
        }
    }
}
