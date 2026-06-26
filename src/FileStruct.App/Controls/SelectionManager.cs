namespace FileStruct.App.Controls;

/// <summary>
/// 十六进制视图的选择状态管理器
/// 跟踪鼠标/键盘选择的字节范围
/// </summary>
public class SelectionManager
{
    private long _startOffset;
    private long _endOffset;
    private bool _hasSelection;

    /// <summary>选择起始偏移</summary>
    public long StartOffset => _startOffset;

    /// <summary>选择结束偏移</summary>
    public long EndOffset => _endOffset;

    /// <summary>选择长度（字节）</summary>
    public long Length => _hasSelection ? Math.Abs(_endOffset - _startOffset) + 1 : 0;

    /// <summary>是否有活动选择</summary>
    public bool HasSelection => _hasSelection;

    /// <summary>选择变更事件</summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// 开始选择
    /// </summary>
    public void BeginSelection(long offset)
    {
        _startOffset = offset;
        _endOffset = offset;
        _hasSelection = true;
        NotifyChanged();
    }

    /// <summary>
    /// 扩展选择到指定偏移
    /// </summary>
    public void ExtendSelection(long offset)
    {
        if (!_hasSelection) { BeginSelection(offset); return; }
        _endOffset = offset;
        NotifyChanged();
    }

    /// <summary>
    /// 清除选择
    /// </summary>
    public void ClearSelection()
    {
        if (!_hasSelection) return;
        _hasSelection = false;
        NotifyChanged();
    }

    /// <summary>
    /// 原子设置选择范围（只触发一次事件，替代 Clear+Begin+Extend 三次触发）
    /// </summary>
    public void SetSelection(long startOffset, long endOffset)
    {
        _startOffset = startOffset;
        _endOffset = endOffset;
        _hasSelection = true;
        NotifyChanged();
    }

    /// <summary>
    /// 判断指定偏移是否在选择范围内
    /// </summary>
    public bool IsSelected(long offset)
    {
        if (!_hasSelection) return false;
        var min = Math.Min(_startOffset, _endOffset);
        var max = Math.Max(_startOffset, _endOffset);
        return offset >= min && offset <= max;
    }

    private void NotifyChanged()
    {
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(
            StartOffset, EndOffset, Length));
    }
}

/// <summary>
/// 选择变更事件参数
/// </summary>
public class SelectionChangedEventArgs : EventArgs
{
    public long StartOffset { get; }
    public long EndOffset { get; }
    public long Length { get; }

    public SelectionChangedEventArgs(long start, long end, long length)
    {
        StartOffset = start;
        EndOffset = end;
        Length = length;
    }
}
