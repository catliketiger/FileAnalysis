using System.Windows;
using System.Windows.Controls;
using FileStruct.App.ViewModels;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class BookmarkBarView : UserControl
{
    public BookmarkBarView()
    {
        InitializeComponent();
    }

    private void BookmarkList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is Bookmark bookmark)
        {
            // 查找主窗口并跳转到书签偏移
            var win = Window.GetWindow(this);
            if (win?.DataContext is MainViewModel mainVm)
            {
                mainVm.HexEditor.ScrollOffset = (bookmark.Offset / 16) * 16;
                mainVm.HexEditor.SelectionInfo = $"书签: {bookmark.Name} @ 0x{bookmark.Offset:X}";
            }
        }
    }
}
