using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileStruct.App.ViewModels;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class BookmarkBarView : UserControl
{
    private DateTime _lastClickTime;

    public BookmarkBarView()
    {
        InitializeComponent();
        BookmarkList.PreviewMouseLeftButtonDown += OnBookmarkPreviewClick;
    }

    private void OnBookmarkPreviewClick(object sender, MouseButtonEventArgs e)
    {
        // 通过 OriginalSource 找到所在的 ListBoxItem
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);

        if (element is not ListBoxItem item || item.DataContext is not Bookmark bookmark)
            return;

        // 双击检测：两次点击间隔 < 500ms
        var now = DateTime.UtcNow;
        var isDoubleClick = (now - _lastClickTime).TotalMilliseconds < 500;
        _lastClickTime = now;

        if (!isDoubleClick) return;

        // 跳转到书签偏移
        var win = Window.GetWindow(this);
        if (win?.DataContext is MainViewModel mainVm)
        {
            mainVm.HexEditor.NavigateToOffset = bookmark.Offset;
            mainVm.HexEditor.NavigateToLength = 1;
            mainVm.HexEditor.SelectionInfo = $"书签: {bookmark.Name} @ 0x{bookmark.Offset:X}";
            mainVm.StatusText = $"已跳转到书签: {bookmark.Name}";
        }

        e.Handled = true;
    }
}
