using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileStruct.App.ViewModels;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class BookmarkBarView : UserControl
{
    public BookmarkBarView()
    {
        InitializeComponent();
    }

    private void BookmarkList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 从点击位置向上查找 ListBoxItem
        var element = e.OriginalSource as FrameworkElement;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element) as FrameworkElement;

        if (element is ListBoxItem item && item.DataContext is Bookmark bookmark)
        {
            var win = Window.GetWindow(this);
            if (win?.DataContext is MainViewModel mainVm)
            {
                mainVm.HexEditor.NavigateToOffset = bookmark.Offset;
                mainVm.HexEditor.NavigateToLength = 1;
                mainVm.HexEditor.SelectionInfo = $"书签: {bookmark.Name} @ 0x{bookmark.Offset:X}";
                mainVm.StatusText = $"已跳转到书签: {bookmark.Name}";
            }
        }
    }
}
