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

    public static readonly DependencyProperty NavigateToOffsetProperty =
        DependencyProperty.Register(nameof(NavigateToOffset), typeof(long),
            typeof(HexView), new PropertyMetadata(-1L, OnNavigateToOffsetChanged));

    public static readonly DependencyProperty NavigateToLengthProperty =
        DependencyProperty.Register(nameof(NavigateToLength), typeof(int),
            typeof(HexView), new PropertyMetadata(1));

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

    public long NavigateToOffset
    {
        get => (long)GetValue(NavigateToOffsetProperty);
        set => SetValue(NavigateToOffsetProperty, value);
    }

    public int NavigateToLength
    {
        get => (int)GetValue(NavigateToLengthProperty);
        set => SetValue(NavigateToLengthProperty, value);
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
        var hlBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 66, 133, 244));
        var normalBrush = System.Windows.Media.Brushes.Transparent;

        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item && item.Content is HexRowData)
            {
                // 遍历行内的所有字节 Border 元素
                ApplyByteHighlights(item, selStart, selEnd, hlBrush, normalBrush);
            }
        }
    }

    private static void ApplyByteHighlights(System.Windows.DependencyObject parent, long selStart, long selEnd,
        System.Windows.Media.Brush hlBrush, System.Windows.Media.Brush normalBrush)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.Border border && border.DataContext is ByteCell cell)
            {
                border.Background = (selStart >= 0 && cell.Offset >= selStart && cell.Offset <= selEnd)
                    ? hlBrush : normalBrush;
            }
            else
            {
                ApplyByteHighlights(child, selStart, selEnd, hlBrush, normalBrush);
            }
        }
    }

    private static long? GetClickedByteOffset(System.Windows.DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.FrameworkElement fe && fe.DataContext is ByteCell cell)
                return cell.Offset;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void OnListBoxPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Buffer == null) return;

        var offset = GetClickedByteOffset(e.OriginalSource as System.Windows.DependencyObject);
        if (offset.HasValue)
        {
            Selection.BeginSelection(offset.Value);
            _isDragging = true;
            e.Handled = true;
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

        var offset = GetClickedByteOffset(e.OriginalSource as System.Windows.DependencyObject);
        if (offset.HasValue)
        {
            Selection.ExtendSelection(offset.Value);
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

    private static void OnNavigateToOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view && e.NewValue is long offset && offset >= 0)
            view.NavigateTo(offset, view.NavigateToLength);
    }

    /// <summary>
    /// 导航到指定偏移：居中显示 + 高亮指定长度的字节
    /// </summary>
    public void NavigateTo(long offset, int length = 1)
    {
        if (Buffer == null || RowList == null) return;

        // 设置选中区间
        Selection.ClearSelection();
        Selection.BeginSelection(offset);
        if (length > 1) Selection.ExtendSelection(offset + length - 1);

        // 居中显示
        var listBox = GetTemplateChild("PART_ListBox") as ListBox;
        var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
        if (scrollViewer != null)
        {
            var rowIndex = (int)(offset / BytesPerRow);
            var viewportRows = Math.Max(1, (int)(scrollViewer.ViewportHeight / 20)); // ~20px/行
            var targetRow = Math.Max(0, rowIndex - viewportRows / 2);
            scrollViewer.ScrollToVerticalOffset(targetRow);
        }
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject? parent) where T : System.Windows.DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
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

        // 逐个字节数据（用于字节级高亮和精确点击）
        var byteCells = new ByteCell[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byteCells[i] = new ByteCell
            {
                Hex = data[i].ToString("X2"),
                Offset = offset + i,
            };
        }

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
            Bytes = byteCells,
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
    public ByteCell[] Bytes { get; init; } = [];
}

/// <summary>
/// 单个字节显示数据
/// </summary>
public class ByteCell
{
    public string Hex { get; init; } = "";
    public long Offset { get; init; }
}
