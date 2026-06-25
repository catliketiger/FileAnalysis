using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileStruct.App.ViewModels;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class BookmarkBarView : UserControl
{
    private DateTime _lastClick;

    public BookmarkBarView()
    {
        InitializeComponent();
        // 使用 PreviewMouseLeftButtonUp 避免 ListBox 内置事件处理干扰
        BookmarkList.PreviewMouseLeftButtonUp += OnBookmarkClick;
    }

    private void OnBookmarkClick(object sender, MouseButtonEventArgs e)
    {
        if (BookmarkList.SelectedItem is not Bookmark bookmark) return;

        var now = DateTime.UtcNow;
        var isDoubleClick = (now - _lastClick).TotalMilliseconds < 500;
        _lastClick = now;

        if (!isDoubleClick) return;

        var win = Window.GetWindow(this);
        if (win?.DataContext is MainViewModel vm)
        {
            vm.HexEditor.NavigateToOffset = bookmark.Offset;
            vm.HexEditor.NavigateToLength = 1;
            vm.HexEditor.SelectionInfo = $"书签: {bookmark.Name} @ 0x{bookmark.Offset:X}";
            vm.StatusText = $"已跳转到书签: {bookmark.Name}";
            e.Handled = true;
        }
    }
}
