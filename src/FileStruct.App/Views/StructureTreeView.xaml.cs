using System.Windows;
using System.Windows.Controls;
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
            // 计算目标滚动偏移（对齐到行首）
            var scrollOffset = (node.Offset / 16) * 16;
            mainVm.HexEditor.ScrollOffset = scrollOffset;
            mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
        }
    }
}
