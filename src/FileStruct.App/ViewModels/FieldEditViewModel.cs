using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 字段编辑对话框 ViewModel — 添加/编辑结构树字段
/// </summary>
public partial class FieldEditViewModel : ObservableObject
{
    /// <summary>所有可选的数据类型列表</summary>
    public static FieldDataType[] AllDataTypes { get; } = Enum.GetValues<FieldDataType>();

    /// <summary>所有可选字节序列表</summary>
    public static FieldEndianness[] AllEndianness { get; } = Enum.GetValues<FieldEndianness>();

    // ===== 编辑状态 =====
    private bool _isNew;

    // ===== 字段属性 =====

    [ObservableProperty]
    private string _fieldName = "";

    [ObservableProperty]
    private string _offsetHex = "0x0";

    [ObservableProperty]
    private long _fieldLength = 1;

    [ObservableProperty]
    private FieldDataType _dataType = FieldDataType.Bytes;

    [ObservableProperty]
    private FieldEndianness _endianness = FieldEndianness.LittleEndian;

    /// <summary>是否为新建模式（否则为编辑模式）</summary>
    public bool IsNew => _isNew;

    /// <summary>窗口标题</summary>
    public string WindowTitle => _isNew ? "添加字段" : "编辑字段";

    [ObservableProperty]
    private string _errorText = "";

    /// <summary>解析得到的偏移量</summary>
    public long ParsedOffset
    {
        get
        {
            var text = OffsetHex?.Replace(" ", "") ?? "0x0";
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out var val)
                    ? val : 0;
            }
            return long.TryParse(text, out var dec) ? dec : 0;
        }
    }

    /// <summary>为新建模式初始化</summary>
    public static FieldEditViewModel ForNew(StructureNode? parent)
    {
        var vm = new FieldEditViewModel { _isNew = true };
        if (parent != null)
        {
            var nextOff = parent.Offset + parent.Length;
            vm.OffsetHex = $"0x{nextOff:X}";
            vm.FieldName = $"field_{nextOff:X}";
        }
        return vm;
    }

    /// <summary>为编辑模式初始化</summary>
    public static FieldEditViewModel ForEdit(StructureNode node)
    {
        var vm = new FieldEditViewModel { _isNew = false };
        vm.FieldName = node.Name;
        vm.OffsetHex = $"0x{node.Offset:X}";
        vm.FieldLength = Math.Max(1, node.Length);
        vm.DataType = node.DataType;
        vm.Endianness = node.Endianness;
        return vm;
    }

    // ===== 命令 =====

    [RelayCommand]
    private void Ok()
    {
        if (string.IsNullOrWhiteSpace(FieldName))
        {
            ErrorText = "请输入字段名称";
            return;
        }
        if (FieldLength <= 0)
        {
            ErrorText = "数据长度必须大于 0";
            return;
        }
        CloseDialog(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseDialog(false);
    }

    private void CloseDialog(bool result)
    {
        foreach (Window win in Application.Current.Windows)
        {
            if (win.DataContext == this)
            {
                win.DialogResult = result;
                win.Close();
                return;
            }
        }
    }
}
