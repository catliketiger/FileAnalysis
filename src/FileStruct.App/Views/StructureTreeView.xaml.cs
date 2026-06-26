using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class StructureTreeView : UserControl
{
    private bool _isSyncingSelection;

    public StructureTreeView()
    {
        InitializeComponent();
        StructTree.SelectedItemChanged += OnSelectedItemChanged;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // 重入守卫：防止 Search→选中→Hex导航→Hex选择变化→SelectNodeByOffset→重入SelectedItemChanged
        if (_isSyncingSelection) return;

        if (e.NewValue is TreeItemViewModel item &&
            DataContext is MainViewModel mainVm)
        {
            _isSyncingSelection = true;
            try
            {
                var node = item.Node;
                mainVm.StructureTree.SelectedNode = node;
                // 使用 NavigateToOffset 实现居中 + 高亮
                mainVm.HexEditor.NavigateToOffset = node.Offset;
                mainVm.HexEditor.NavigateToLength = (int)Math.Max(1, node.Length);
                mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
                mainVm.StatusText = $"已定位到字段: {node.Name}";
            }
            finally
            {
                _isSyncingSelection = false;
            }
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
                // SearchTree 设置了 found.IsSelected = true，TreeView 绑定自动触发
                // OnSelectedItemChanged 处理 Hex 导航和状态更新
            }
            else
            {
                mainVm.StatusText = $"未找到匹配的字段: {text}";
            }
        }
    }
}
