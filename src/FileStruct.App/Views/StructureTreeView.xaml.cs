using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class StructureTreeView : UserControl
{
    public StructureTreeView()
    {
        InitializeComponent();
        StructTree.SelectedItemChanged += OnSelectedItemChanged;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeItemViewModel item &&
            DataContext is MainViewModel mainVm)
        {
            var node = item.Node;
            var scrollOffset = (node.Offset / 16) * 16;
            mainVm.HexEditor.ScrollOffset = scrollOffset;
            mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel mainVm)
        {
            var found = mainVm.StructureTree.SearchTree(SearchBox.Text);
            if (!found)
                mainVm.StatusText = $"未找到匹配的字段: {SearchBox.Text}";
        }
    }
}
