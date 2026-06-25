using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.Services.AuxTools;

namespace FileStruct.App.ViewModels;

public partial class AuxToolsViewModel : ObservableObject
{
    private readonly BaseConverter _converter = new();

    [ObservableProperty] private string _inputValue = "";
    [ObservableProperty] private string _decimalValue = "";
    [ObservableProperty] private string _hexValue = "";
    [ObservableProperty] private string _binaryValue = "";
    [ObservableProperty] private string _errorMessage = "";

    [RelayCommand]
    private void Convert()
    {
        if (string.IsNullOrWhiteSpace(InputValue))
        {
            ErrorMessage = "请输入数值";
            return;
        }
        try
        {
            var result = _converter.ConvertAll(InputValue);
            DecimalValue = result.Decimal.ToString("N0");
            HexValue = result.Hex;
            BinaryValue = result.Binary;
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            DecimalValue = HexValue = BinaryValue = "";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputValue = DecimalValue = HexValue = BinaryValue = ErrorMessage = "";
    }
}
