using System.Windows;
using FileStruct.App.ViewModels;

namespace FileStruct.App.Views;

public partial class FieldEditDialog : Window
{
    public FieldEditDialog(FieldEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Owner = Application.Current.MainWindow;
    }
}
