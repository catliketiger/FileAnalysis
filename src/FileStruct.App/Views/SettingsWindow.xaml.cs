using System.Windows;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Owner = Application.Current.MainWindow;

        var cfg = _vm.Config.GetConfig();
        GroupSizeCombo.SelectedIndex = cfg.FileDefaults.DefaultByteGroupSize switch { 1 => 0, 2 => 1, 4 => 2, 8 => 3, _ => 1 };
        EndianCombo.SelectedIndex = cfg.FileDefaults.DefaultEndianness == "BigEndian" ? 1 : 0;
        DarkModeCheck.IsChecked = cfg.UI.Theme != "Light";
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var cfg = _vm.Config.GetConfig();
        cfg.FileDefaults.DefaultByteGroupSize = GroupSizeCombo.SelectedIndex switch { 0 => 1, 1 => 2, 2 => 4, 3 => 8, _ => 2 };
        cfg.FileDefaults.DefaultEndianness = EndianCombo.SelectedIndex == 1 ? "BigEndian" : "LittleEndian";
        cfg.UI.Theme = DarkModeCheck.IsChecked == true ? "Dark" : "Light";
        _vm.Config.UpdateConfig(cfg);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
