namespace FileStruct.Core.Models;

/// <summary>
/// 书签：标记文件中的关键偏移位置，支持一键跳转
/// </summary>
public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>书签名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>字节偏移</summary>
    public long Offset { get; set; }

    /// <summary>书签描述（可选）</summary>
    public string? Description { get; set; }

    /// <summary>创建时间 (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>标签颜色（可选，格式如 "#FFFFEB3B"）</summary>
    public string? Color { get; set; }
}
