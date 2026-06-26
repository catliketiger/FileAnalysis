using System.Windows;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Owner = Application.Current.MainWindow;
    }
}
