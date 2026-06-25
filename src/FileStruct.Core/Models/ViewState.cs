namespace FileStruct.Core.Models;

/// <summary>
/// 视图状态：记录当前视图的显示位置和设置，用于视图切换时保留状态
/// </summary>
public class ViewState
{
    /// <summary>当前活动视图 ("Hex" 或 "Text")</summary>
    public string ActiveView { get; set; } = "Hex";

    /// <summary>当前滚动偏移（字节）</summary>
    public long ScrollOffset { get; set; }

    /// <summary>选择起始偏移（可选）</summary>
    public long? SelectionStart { get; set; }

    /// <summary>选择结束偏移（可选）</summary>
    public long? SelectionEnd { get; set; }

    /// <summary>字节分组大小 (1/2/4/8)</summary>
    public int ByteGroupSize { get; set; } = 2;

    /// <summary>是否小端字节序</summary>
    public bool IsLittleEndian { get; set; } = true;

    /// <summary>文本编码名称（如 "UTF-8"）</summary>
    public string? TextEncoding { get; set; }
}
