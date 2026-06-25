using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections;
using FileStruct.Core.Models;

namespace FileStruct.App.Controls;

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
            typeof(HexView), new PropertyMetadata(2, OnByteGroupSizeChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(long),
            typeof(HexView), new PropertyMetadata(0L, OnScrollOffsetChanged));

    public static readonly DependencyProperty SelectionStartProperty =
        DependencyProperty.Register(nameof(SelectionStart), typeof(long),
            typeof(HexView), new PropertyMetadata(-1L));

    public static readonly DependencyProperty SelectionEndProperty =
        DependencyProperty.Register(nameof(SelectionEnd), typeof(long),
            typeof(HexView), new PropertyMetadata(-1L));

    public BinaryBuffer? Buffer
    {
        get => (BinaryBuffer?)GetValue(BufferProperty);
        set => SetValue(BufferProperty, value);
    }

    public int BytesPerRow
    {
        get => (int)GetValue(BytesPerRowProperty);
        set => SetValue(BytesPerRowProperty, value);
    }

    public int ByteGroupSize
    {
        get => (int)GetValue(ByteGroupSizeProperty);
        set => SetValue(ByteGroupSizeProperty, value);
    }

    public long ScrollOffset
    {
        get => (long)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public long SelectionStart
    {
        get => (long)GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public long SelectionEnd
    {
        get => (long)GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    #endregion

    #region 属性

    public SelectionManager Selection { get; }

    /// <summary>行数据源（供虚拟化 ItemsControl 使用）</summary>
    public HexRowList? RowList { get; private set; }

    #endregion

    #region 方法

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        var listBox = GetTemplateChild("PART_ListBox") as ListBox;
        if (listBox != null)
        {
            listBox.PreviewMouseLeftButtonDown += OnListBoxPreviewMouseDown;
            listBox.MouseMove += OnListBoxMouseMove;
            listBox.MouseLeftButtonUp += OnListBoxMouseUp;
            listBox.PreviewMouseLeftButtonDown += OnListBoxEmptyClick;
            listBox.ItemContainerGenerator.StatusChanged += (_, _) => UpdateRowHighlights();
        }
        // 选择变更时更新高亮
        Selection.SelectionChanged += (_, args) =>
        {
            SelectionStart = Selection.HasSelection ? Math.Min(args.StartOffset, args.EndOffset) : -1;
            SelectionEnd = Selection.HasSelection ? Math.Max(args.StartOffset, args.EndOffset) : -1;
            UpdateRowHighlights();
        };
        RebuildRows();
    }

    private bool _isDragging;

    private void OnListBoxEmptyClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 点击空白处取消选择
        var element = e.OriginalSource as System.Windows.FrameworkElement;
        while (element != null && element is not ListBoxItem && element is not ListBox)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element) as System.Windows.FrameworkElement;

        if (element is ListBox && !Selection.HasSelection) return;
        if (element is not ListBoxItem)
        {
            Selection.ClearSelection();
            SelectionStart = -1;
            SelectionEnd = -1;
            UpdateRowHighlights();
        }
    }

    private void UpdateRowHighlights()
    {
        var listBox = GetTemplateChild("PART_ListBox") as ListBox;
        if (listBox == null) return;

        var selStart = SelectionStart;
        var selEnd = SelectionEnd;

        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item && item.Content is HexRowData row)
            {
                var rowStart = row.RowOffset;
                var rowEnd = rowStart + row.RowByteCount;
                var highlighted = selStart >= 0 && rowStart < selEnd && rowEnd > selStart;
                item.Background = highlighted
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 66, 133, 244))
                    : System.Windows.Media.Brushes.Transparent;
            }
        }
    }

    private void OnListBoxPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Buffer == null) return;

        // 获取点击的 ListBoxItem
        var element = e.OriginalSource as System.Windows.FrameworkElement;
        while (element != null && element is not ListBoxItem)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element) as System.Windows.FrameworkElement;

        if (element is ListBoxItem item && item.Content is HexRowData rowData)
        {
            var rowOffset = rowData.RowOffset;
            var rowLen = rowData.RowByteCount;
            if (rowLen <= 0) return;

            // 根据点击位置估算字节偏移
            var pos = e.GetPosition(item);
            var bytesPerRow = BytesPerRow;

            // 偏移列约 80px，十六进制列从 80px 开始
            if (pos.X > 80)
            {
                int byteIndex;
                if (pos.X < 460) // Hex 列区域
                {
                    // 每个字节约 23px (根据分组大小调整)
                    var hexStartX = 80.0;
                    var byteWidth = bytesPerRow <= 16 ? 460.0 / bytesPerRow : 480.0 / bytesPerRow;
                    byteIndex = (int)((pos.X - hexStartX) / byteWidth);
                }
                else // ASCII 列区域
                {
                    var asciiStartX = 470.0;
                    var charWidth = 8.0;
                    byteIndex = (int)((pos.X - asciiStartX) / charWidth);
                }

                byteIndex = Math.Clamp(byteIndex, 0, rowLen - 1);
                var absoluteOffset = rowOffset + byteIndex;

                Selection.BeginSelection(absoluteOffset);
                _isDragging = true;
                e.Handled = true;
            }
        }
    }

    private void OnListBoxMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || Buffer == null) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            _isDragging = false;
            return;
        }

        var element = e.OriginalSource as System.Windows.FrameworkElement;
        while (element != null && element is not ListBoxItem)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element) as System.Windows.FrameworkElement;

        if (element is ListBoxItem item && item.Content is HexRowData rowData)
        {
            var pos = e.GetPosition(item);
            var rowOffset = rowData.RowOffset;
            var rowLen = rowData.RowByteCount;
            if (rowLen <= 0) return;

            var byteWidth = 460.0 / BytesPerRow;
            var byteIndex = Math.Clamp((int)((pos.X - 80) / byteWidth), 0, rowLen - 1);
            Selection.ExtendSelection(rowOffset + byteIndex);
        }
    }

    private void OnListBoxMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDragging = false;
    }

    private void RebuildRows()
    {
        var listBox = GetTemplateChild("PART_ListBox") as ListBox;

        if (Buffer == null)
        {
            RowList = null;
            if (listBox != null) listBox.ItemsSource = null;
            return;
        }

        RowList = new HexRowList(Buffer, BytesPerRow, ByteGroupSize);
        if (listBox != null)
        {
            listBox.ItemsSource = RowList;
        }
    }

    private void ScrollToRow(long offset)
    {
        if (RowList == null || Buffer == null) return;
        var rowIndex = (int)(offset / BytesPerRow);
        if (rowIndex < 0 || rowIndex >= RowList.Count) return;

        var listBox = GetTemplateChild("PART_ListBox") as ListBox;
        listBox?.ScrollIntoView(RowList[rowIndex]);
    }

    #endregion

    #region 事件处理

    private static void OnBufferChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view)
        {
            view.ScrollOffset = 0;
            view.Selection.ClearSelection();
            view.RebuildRows();
        }
    }

    private static void OnByteGroupSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view)
            view.RebuildRows();
    }

    private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view && e.NewValue is long offset)
        {
            view.ScrollToRow(offset);
        }
    }

    #endregion
}

/// <summary>
/// 虚拟化行数据源 — 不预加载所有行，按需从 BinaryBuffer 读取
/// </summary>
public class HexRowList : IList
{
    private readonly BinaryBuffer _buffer;
    private readonly int _bytesPerRow;
    private readonly int _byteGroupSize;

    public HexRowList(BinaryBuffer buffer, int bytesPerRow, int byteGroupSize)
    {
        _buffer = buffer;
        _bytesPerRow = bytesPerRow;
        _byteGroupSize = byteGroupSize;
    }

    public int Count => (int)((_buffer.Length + _bytesPerRow - 1) / _bytesPerRow);
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public bool IsFixedSize => true;
    public bool IsReadOnly => true;

    public object? this[int index]
    {
        get => index >= 0 && index < Count ? BuildRow(index) : null;
        set => throw new NotSupportedException();
    }

    private HexRowData BuildRow(int rowIndex)
    {
        var offset = rowIndex * _bytesPerRow;
        var count = (int)Math.Min(_bytesPerRow, _buffer.Length - offset);
        var data = count > 0 ? _buffer.ReadBytes(offset, count) : [];

        // 偏移列
        var offsetStr = $"{offset:X8}";

        // 十六进制列
        var hexParts = new List<string>();
        for (int i = 0; i < _bytesPerRow; i += _byteGroupSize)
        {
            var sb = new System.Text.StringBuilder();
            for (int j = 0; j < _byteGroupSize && i + j < data.Length; j++)
                sb.Append(data[i + j].ToString("X2"));
            hexParts.Add(sb.ToString());
        }
        var hexStr = string.Join(" ", hexParts);

        // ASCII 列
        var asciiChars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
            asciiChars[i] = data[i] >= 0x20 && data[i] <= 0x7E ? (char)data[i] : '.';

        return new HexRowData
        {
            RowOffset = offset,
            OffsetString = offsetStr,
            HexString = hexStr,
            AsciiString = new string(asciiChars),
            RowByteCount = data.Length,
        };
    }

    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() { }
    public bool Contains(object? value) => false;
    public int IndexOf(object? value) => -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    public void CopyTo(Array array, int index) { }
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return BuildRow(i);
    }
}

/// <summary>
/// 单行十六进制数据（不可变 DTO）
/// </summary>
public class HexRowData
{
    public long RowOffset { get; init; }
    public string OffsetString { get; init; } = "";
    public string HexString { get; init; } = "";
    public string AsciiString { get; init; } = "";
    public int RowByteCount { get; init; }
    public bool IsCompleteRow => RowByteCount >= 16;
}
