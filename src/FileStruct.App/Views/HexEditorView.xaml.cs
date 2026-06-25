using System.Windows.Controls;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class HexEditorView : UserControl
{
    public HexEditorView()
    {
        InitializeComponent();
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
