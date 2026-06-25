using System.Text;

namespace FileStruct.Core.Models;

/// <summary>
/// 文件类型检测结果
/// </summary>
public readonly struct FileTypeInfo
{
    public FileTypeInfo(FileCategory category, string extension, string displayName,
        bool isText = false, string? encodingName = null, string? mimeType = null)
    {
        Category = category;
        Extension = extension;
        DisplayName = displayName;
        IsText = isText;
        EncodingName = encodingName;
        MimeType = mimeType;
    }

    /// <summary>
    /// 文件大类
    /// </summary>
    public FileCategory Category { get; }

    /// <summary>
    /// 文件扩展名（含点号，如 ".txt"），无扩展名则为空字符串
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// 人类可读的类型名称（已本地化）
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 是否为纯文本格式
    /// </summary>
    public bool IsText { get; }

    /// <summary>
    /// MIME 类型（可选）
    /// </summary>
    public string? MimeType { get; }

    /// <summary>
    /// 检测到的文本编码名称（仅 IsText=true 时有值）
    /// 例如 "utf-8", "utf-16LE", "gb2312"
    /// </summary>
    public string? EncodingName { get; }

    /// <summary>
    /// 获取文本编码对象（从 EncodingName 解析）
    /// </summary>
    public Encoding? GetEncoding()
    {
        if (EncodingName == null) return null;
        try { return Encoding.GetEncoding(EncodingName); }
        catch { return null; }
    }
}

/// <summary>
/// 文件大类分类
/// </summary>
public enum FileCategory
{
    /// <summary>未知类型</summary>
    Unknown,

    /// <summary>二进制文件（不可直接以文本阅读）</summary>
    Binary,

    /// <summary>纯文本文件</summary>
    Text,

    /// <summary>可执行文件</summary>
    Executable,

    /// <summary>压缩/归档文件</summary>
    Archive,

    /// <summary>图片文件</summary>
    Image,

    /// <summary>音频文件</summary>
    Audio,

    /// <summary>视频文件</summary>
    Video,

    /// <summary>文档文件</summary>
    Document,
}
