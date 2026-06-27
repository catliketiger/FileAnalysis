using System.Windows;
using System.Windows.Controls;
using FileStruct.App.ViewModels;

namespace FileStruct.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void VolumeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is VolumeListItem item &&
            DataContext is MainViewModel vm)
        {
            if (!item.IsCurrent)
                vm.SwitchToVolume(item.FullPath, item.VolumeIndex);
        }
    }
}
