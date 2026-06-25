using System.Windows.Controls;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class TextView : UserControl
{
    public TextView()
    {
        InitializeComponent();
    }

    private void EncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item &&
            item.Tag is string encodingName &&
            DataContext is MainViewModel mainVm)
        {
            mainVm.TextView.ReloadByEncodingName(encodingName);
        }
    }
}
