namespace FileStruct.Core.Models;

/// <summary>
/// 项目文件：保存分析状态的顶层聚合
/// </summary>
public class ProjectFile
{
    /// <summary>当前项目文件版本号</summary>
    public const string CurrentVersion = "1.1.0";

    /// <summary>项目格式版本，用于向后兼容</summary>
    public string Version { get; set; } = CurrentVersion;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后修改时间</summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>源文件信息</summary>
    public SourceFileInfo SourceFile { get; set; } = new();

    /// <summary>视图状态</summary>
    public ViewState ViewState { get; set; } = new();

    /// <summary>书签列表</summary>
    public List<Bookmark> Bookmarks { get; set; } = new();

    /// <summary>用户备注列表</summary>
    public List<UserNote> Notes { get; set; } = new();

    /// <summary>结构树根节点（识别出的结构字段）</summary>
    public StructureNode? StructureRoot { get; set; }
}

/// <summary>
/// 源文件元信息
/// </summary>
public class SourceFileInfo
{
    /// <summary>原始文件路径（用户机器上的路径，仅供参考）</summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>SHA256 哈希值，用于校验文件一致性</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>检测到的文件类型（可选）</summary>
    public FileTypeInfo? DetectedType { get; set; }
}
