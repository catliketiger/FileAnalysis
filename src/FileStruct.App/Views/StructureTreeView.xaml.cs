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
            // 使用 NavigateToOffset 实现居中 + 高亮
            mainVm.HexEditor.NavigateToOffset = node.Offset;
            mainVm.HexEditor.NavigateToLength = (int)Math.Max(1, node.Length);
            mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
            mainVm.StatusText = $"已定位到字段: {node.Name}";
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
