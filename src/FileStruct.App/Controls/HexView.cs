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
        InitHexHeader();
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

    public static readonly DependencyProperty HexHeaderItemsProperty =
        DependencyProperty.Register(nameof(HexHeaderItems), typeof(ByteCell[]),
            typeof(HexView), new PropertyMetadata(null));

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
            typeof(HexView), new PropertyMetadata(1, OnNavigateToLengthChanged));

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

    /// <summary>十六进制列顶部的行内偏移标题（0~F）</summary>
    public ByteCell[]? HexHeaderItems
    {
        get => (ByteCell[]?)GetValue(HexHeaderItemsProperty);
        set => SetValue(HexHeaderItemsProperty, value);
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

    /// <summary>待处理的导航偏移</summary>
    private long _pendingNavigateOffset = -1;
    /// <summary>待处理的导航长度</summary>
    private int _pendingNavigateLength = 1;

    #endregion

    #region 方法

    /// <summary>生成十六进制列顶部的行内偏移标题（0~F）</summary>
    private void InitHexHeader()
    {
        var count = BytesPerRow; // 默认 16
        var items = new ByteCell[count];
        for (int i = 0; i < count; i++)
            items[i] = new ByteCell { Hex = i.ToString("X"), Offset = -1 };
        HexHeaderItems = items;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        var listBox = GetTemplateChild("PART_ListBox") as ListBox;
        if (listBox != null)
        {
            // 移除旧事件避免重复订阅
            listBox.PreviewMouseLeftButtonDown -= OnListBoxPreviewMouseDown;
            listBox.MouseMove -= OnListBoxMouseMove;
            listBox.MouseLeftButtonUp -= OnListBoxMouseUp;
            listBox.PreviewMouseLeftButtonDown -= OnListBoxEmptyClick;
            listBox.ContextMenuOpening -= OnContextMenuOpening;

            listBox.PreviewMouseLeftButtonDown += OnListBoxPreviewMouseDown;
            listBox.MouseMove += OnListBoxMouseMove;
            listBox.MouseLeftButtonUp += OnListBoxMouseUp;
            listBox.PreviewMouseLeftButtonDown += OnListBoxEmptyClick;
            listBox.ContextMenuOpening += OnContextMenuOpening;
            listBox.ItemContainerGenerator.StatusChanged += (_, _) => UpdateRowHighlights();

            // 绑定右键菜单项（先移除再添加防重复）
            if (listBox.ContextMenu != null)
            {
                foreach (var item in listBox.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>())
                {
                    item.Click -= OnContextMenuItemClick;
                    item.Click += OnContextMenuItemClick;
                }
            }
        }
        // 选择变更时更新高亮
        Selection.SelectionChanged -= OnSelectionChanged;
        Selection.SelectionChanged += OnSelectionChanged;
        RebuildRows();
    }

    private void OnSelectionChanged(object? sender, Controls.SelectionChangedEventArgs args)
    {
        SelectionStart = Selection.HasSelection ? Math.Min(args.StartOffset, args.EndOffset) : -1;
        SelectionEnd = Selection.HasSelection ? Math.Max(args.StartOffset, args.EndOffset) : -1;
        UpdateRowHighlights();
    }

    private bool _isDragging;
    private long _selectionAnchor = -1;
    private long _contextMenuOffset = -1;

    /// <summary>请求添加书签事件（供 HexEditorView 订阅）</summary>
    public event Action<long>? BookmarkRequested;

    /// <summary>请求从选中范围创建字段（供 HexEditorView 订阅）</summary>
    public event Action<long, long>? CreateFieldRequested;

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

        // 只遍历已生成的可见容器（大文件有数亿行，绝不能遍历 listBox.Items.Count）
        var generator = listBox.ItemContainerGenerator;
        if (generator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            return;

        // 仅遍历可见行范围（大文件数亿行，绝不能遍历所有）
        var sv = FindVisualChild<ScrollViewer>(listBox);
        if (sv == null) return;
        int firstVisible = (int)(sv.VerticalOffset);
        int viewportRows = Math.Max(1, (int)(sv.ViewportHeight));
        int lastVisible = firstVisible + viewportRows + 1;
        for (int i = firstVisible; i <= lastVisible; i++)
        {
            if (generator.ContainerFromIndex(i) is ListBoxItem item && item.Content is HexRowData)
                ApplyByteHighlights(item, selStart, selEnd, hlBrush, normalBrush);
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

    private void OnContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        // 获取右键点击位置的字节偏移
        var listBox = sender as System.Windows.Controls.ListBox;
        if (listBox == null) return;
        var pos = System.Windows.Input.Mouse.GetPosition(listBox);
        var hit = System.Windows.Media.VisualTreeHelper.HitTest(listBox, pos);
        if (hit?.VisualHit == null) return;

        _contextMenuOffset = -1;
        var dep = hit.VisualHit as System.Windows.DependencyObject;
        while (dep != null && _contextMenuOffset < 0)
        {
            if (dep is System.Windows.FrameworkElement fe && fe.DataContext is ByteCell cell)
                _contextMenuOffset = cell.Offset;
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        }
    }

    private void OnContextMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_contextMenuOffset < 0) return;

        var header = (sender as System.Windows.Controls.MenuItem)?.Header?.ToString();
        switch (header)
        {
            case "选区开始":
                _selectionAnchor = _contextMenuOffset;
                Selection.BeginSelection(_contextMenuOffset);
                UpdateRowHighlights();
                break;

            case "选区结束":
                if (_selectionAnchor >= 0)
                {
                    Selection.ClearSelection();
                    Selection.BeginSelection(Math.Min(_selectionAnchor, _contextMenuOffset));
                    Selection.ExtendSelection(Math.Max(_selectionAnchor, _contextMenuOffset));
                    _selectionAnchor = -1;
                    UpdateRowHighlights();
                }
                break;

            case "创建字段":
                if (Selection.HasSelection && Buffer != null)
                {
                    var start = Math.Min(Selection.StartOffset, Selection.EndOffset);
                    var end = Math.Max(Selection.StartOffset, Selection.EndOffset);
                    CreateFieldRequested?.Invoke(start, end - start + 1);
                }
                break;

            case "添加书签":
                BookmarkRequested?.Invoke(_contextMenuOffset);
                break;
        }
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

    private static void DoNavigate(HexView view, long offset, int len)
    {
        if (offset >= 0 && len > 0)
            view.Dispatcher.BeginInvoke(
                new Action(() => view.NavigateTo(offset, len)),
                System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void OnNavigateToLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view && e.NewValue is int len && len >= 0)
        {
            view._pendingNavigateLength = Math.Max(1, len);
            DoNavigate(view, view.NavigateToOffset, Math.Max(1, len));
        }
    }

    private static void OnNavigateToOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexView view && e.NewValue is long offset && offset >= 0)
        {
            view._pendingNavigateOffset = offset;
            DoNavigate(view, offset, view._pendingNavigateLength);
        }
    }

    /// <summary>
    /// 导航到指定偏移：居中显示 + 高亮指定长度的字节
    /// </summary>
    public void NavigateTo(long offset, int length = 1)
    {
        if (Buffer == null || RowList == null) return;

        var selEnd = length > 1 ? offset + length - 1 : offset;

        // 临时退订事件，避免滚动前触发高亮（会和目标位置不一致）
        Selection.SelectionChanged -= OnSelectionChanged;
        try
        {
            Selection.SetSelection(offset, selEnd);
            // 直接设 DP，后续 UpdateRowHighlights 读取这些值
            SelectionStart = Math.Min(offset, selEnd);
            SelectionEnd = Math.Max(offset, selEnd);

            // 居中滚动
            var listBox = GetTemplateChild("PART_ListBox") as ListBox;
            var sv = FindVisualChild<ScrollViewer>(listBox);
            if (sv != null)
            {
                var rowIndex = (int)(offset / BytesPerRow);
                var viewportRows = Math.Max(1, (int)(sv.ViewportHeight));
                var targetRow = Math.Max(0, rowIndex - viewportRows / 2);
                sv.ScrollToVerticalOffset(targetRow);
            }
        }
        finally
        {
            Selection.SelectionChanged += OnSelectionChanged;
        }

        // 等容器生成完毕后刷新高亮
        Dispatcher.InvokeAsync(UpdateRowHighlights, System.Windows.Threading.DispatcherPriority.Loaded);
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
        var byteCells = new ByteCell[_bytesPerRow];
        for (int i = 0; i < data.Length; i++)
        {
            byteCells[i] = new ByteCell { Hex = data[i].ToString("X2"), Offset = offset + i };
        }
        for (int i = data.Length; i < _bytesPerRow; i++)
        {
            byteCells[i] = new ByteCell { Hex = "  ", Offset = -1 };
        }

        // ASCII 列（始终补全到16字符，不足的补空格）
        var asciiChars = new char[_bytesPerRow];
        for (int i = 0; i < data.Length; i++)
            asciiChars[i] = data[i] >= 0x20 && data[i] <= 0x7E ? (char)data[i] : '.';
        for (int i = data.Length; i < _bytesPerRow; i++)
            asciiChars[i] = ' ';

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
    /// <summary>惰性枚举：按需逐个构建行。WPF VirtualizingStackPanel 仅通过索引器访问可见行，不会遍历全部。</summary>
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
