using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using FileStruct.Core.Models;

namespace FileStruct.App.Controls;

/// <summary>
/// 虚拟化十六进制编辑器自定义控件
/// 使用 VirtualizingStackPanel 实现仅渲染视口行，支持 200MB 大文件流畅滚动
/// </summary>
public class HexView : Control
{
    static HexView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HexView),
            new FrameworkPropertyMetadata(typeof(HexView)));
    }

    public HexView()
    {
        Selection = new SelectionManager();
    }

    #region 依赖属性

    public static readonly DependencyProperty BufferProperty =
        DependencyProperty.Register(nameof(Buffer), typeof(BinaryBuffer),
            typeof(HexView), new PropertyMetadata(null, OnBufferChanged));

    public static readonly DependencyProperty BytesPerRowProperty =
        DependencyProperty.Register(nameof(BytesPerRow), typeof(int),
            typeof(HexView), new PropertyMetadata(16));

    public static readonly DependencyProperty ByteGroupSizeProperty =
        DependencyProperty.Register(nameof(ByteGroupSize), typeof(int),
            typeof(HexView), new PropertyMetadata(2));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(long),
            typeof(HexView), new PropertyMetadata(0L));

    /// <summary>二进制数据缓冲区</summary>
    public BinaryBuffer? Buffer
    {
        get => (BinaryBuffer?)GetValue(BufferProperty);
        set => SetValue(BufferProperty, value);
    }

    /// <summary>每行显示的字节数</summary>
    public int BytesPerRow
    {
        get => (int)GetValue(BytesPerRowProperty);
        set => SetValue(BytesPerRowProperty, value);
    }

    /// <summary>字节分组大小 (1/2/4/8)</summary>
    public int ByteGroupSize
    {
        get => (int)GetValue(ByteGroupSizeProperty);
        set => SetValue(ByteGroupSizeProperty, value);
    }

    /// <summary>当前滚动偏移（字节）</summary>
    public long ScrollOffset
    {
        get => (long)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    #endregion

    #region 属性

    /// <summary>总行数</summary>
    public long TotalRows => Buffer == null ? 0 :
        (Buffer.Length + BytesPerRow - 1) / BytesPerRow;

    /// <summary>选择管理器</summary>
    public SelectionManager Selection { get; }

    #endregion

    #region 事件

    public event EventHandler<Controls.SelectionChangedEventArgs>? SelectionChanged
    {
        add => Selection.SelectionChanged += value;
        remove => Selection.SelectionChanged -= value;
    }

    #endregion

    #region 方法

    /// <summary>
    /// 获取指定行的字节数据
    /// </summary>
    public byte[] GetRowData(long rowIndex)
    {
        if (Buffer == null) return [];
        var offset = rowIndex * BytesPerRow;
        var count = (int)Math.Min(BytesPerRow, Buffer.Length - offset);
        if (count <= 0) return [];
        return Buffer.ReadBytes(offset, count);
    }

    /// <summary>
    /// 将行索引转换为显示字符串
    /// </summary>
    public string FormatOffset(long rowIndex)
    {
        return $"{rowIndex * BytesPerRow:X8}";
    }

    /// <summary>
    /// 将字节数组格式化为十六进制字符串（含分组空格）
    /// </summary>
    public string FormatHex(byte[] data)
    {
        if (data.Length == 0) return "";
        var parts = new List<string>();
        for (int i = 0; i < data.Length; i += ByteGroupSize)
        {
            var sb = new System.Text.StringBuilder();
            for (int j = 0; j < ByteGroupSize && i + j < data.Length; j++)
            {
                sb.Append(data[i + j].ToString("X2"));
            }
            parts.Add(sb.ToString());
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// 将字节数组格式化为 ASCII 字符串（不可打印字符替换为 .）
    /// </summary>
    public string FormatAscii(byte[] data)
    {
        var chars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
            chars[i] = data[i] >= 0x20 && data[i] <= 0x7E ? (char)data[i] : '.';
        return new string(chars);
    }

    /// <summary>
    /// 将字节按分组大小填充为完整宽度字符串（用于对齐）
    /// </summary>
    public string FormatHexAligned(byte[] data)
    {
        if (data.Length == 0) return "";
        var totalGroupSlots = (BytesPerRow + ByteGroupSize - 1) / ByteGroupSize;
        var filledData = new byte[totalGroupSlots * ByteGroupSize];
        data.CopyTo(filledData, 0);
        return FormatHex(filledData);
    }

    #endregion

    #region 事件处理

    private static void OnBufferChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view)
        {
            view.ScrollOffset = 0;
            view.Selection.ClearSelection();
            view.InvalidateVisual();
        }
    }

    #endregion
}
