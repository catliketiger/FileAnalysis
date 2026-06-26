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
            // 清除 VM 中旧的选中状态，设置新的
            StructureTreeViewModel.ClearAllSelection(mainVm.StructureTree.RootItems);
            item.IsSelected = true;

            var node = item.Node;
            mainVm.StructureTree.SelectedNode = node;
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
            var text = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            var found = mainVm.StructureTree.SearchTree(text);
            if (found)
            {
                mainVm.StatusText = $"已定位到字段: {text}";
                var matchedNode = mainVm.StructureTree.SelectedNode;
                if (matchedNode != null)
                {
                    mainVm.HexEditor.NavigateToOffset = matchedNode.Offset;
                    mainVm.HexEditor.NavigateToLength = (int)Math.Max(1, matchedNode.Length);
                    mainVm.HexEditor.SelectionInfo = $"字段: {matchedNode.Name} @ 0x{matchedNode.Offset:X}";
                }
                // 滚动 TreeView 到选中项
                if (StructTree.ItemContainerGenerator.ContainerFromItem(
                    StructTree.SelectedItem) is TreeViewItem tvi)
                {
                    tvi.BringIntoView();
                }
            }
            else
            {
                mainVm.StatusText = $"未找到匹配的字段: {text}";
            }
        }
    }
}
