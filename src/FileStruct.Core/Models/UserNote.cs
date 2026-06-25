namespace FileStruct.Core.Models;

/// <summary>
/// 用户备注：关联到文件中某个字节区间的注释
/// </summary>
public class UserNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>起始偏移</summary>
    public long Offset { get; set; }

    /// <summary>覆盖的字节长度</summary>
    public long Length { get; set; } = 1;

    /// <summary>备注内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
