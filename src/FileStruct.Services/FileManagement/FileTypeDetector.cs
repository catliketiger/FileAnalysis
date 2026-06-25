using System.Text;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.FileManagement;

/// <summary>
/// 文件类型检测器：基于扩展名和魔数识别文件类型
/// </summary>
public class FileTypeDetector : IFileTypeDetector
{
    // 常见文件扩展名 → 分类映射
    private static readonly Dictionary<string, (FileCategory Category, string DisplayName, string MimeType)> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 文本格式
        [".txt"] = (FileCategory.Text, "纯文本文件", "text/plain"),
        [".md"] = (FileCategory.Text, "Markdown 文件", "text/markdown"),
        [".json"] = (FileCategory.Text, "JSON 文件", "application/json"),
        [".xml"] = (FileCategory.Text, "XML 文件", "application/xml"),
        [".yaml"] = (FileCategory.Text, "YAML 文件", "text/yaml"),
        [".yml"] = (FileCategory.Text, "YAML 文件", "text/yaml"),
        [".csv"] = (FileCategory.Text, "CSV 文件", "text/csv"),
        [".html"] = (FileCategory.Text, "HTML 文件", "text/html"),
        [".htm"] = (FileCategory.Text, "HTML 文件", "text/html"),
        [".css"] = (FileCategory.Text, "CSS 文件", "text/css"),
        [".js"] = (FileCategory.Text, "JavaScript 文件", "text/javascript"),
        [".py"] = (FileCategory.Text, "Python 脚本", "text/x-python"),
        [".ini"] = (FileCategory.Text, "INI 配置文件", "text/plain"),
        [".cfg"] = (FileCategory.Text, "配置文件", "text/plain"),
        [".log"] = (FileCategory.Text, "日志文件", "text/plain"),

        // 可执行文件
        [".exe"] = (FileCategory.Executable, "Windows 可执行文件", "application/x-msdownload"),
        [".dll"] = (FileCategory.Executable, "动态链接库", "application/x-msdownload"),
        [".elf"] = (FileCategory.Executable, "ELF 可执行文件", "application/x-elf"),

        // 压缩/归档
        [".zip"] = (FileCategory.Archive, "ZIP 压缩包", "application/zip"),
        [".rar"] = (FileCategory.Archive, "RAR 压缩包", "application/vnd.rar"),
        [".7z"] = (FileCategory.Archive, "7z 压缩包", "application/x-7z-compressed"),
        [".gz"] = (FileCategory.Archive, "GZip 压缩文件", "application/gzip"),
        [".tar"] = (FileCategory.Archive, "TAR 归档", "application/x-tar"),

        // 图片
        [".png"] = (FileCategory.Image, "PNG 图片", "image/png"),
        [".jpg"] = (FileCategory.Image, "JPEG 图片", "image/jpeg"),
        [".jpeg"] = (FileCategory.Image, "JPEG 图片", "image/jpeg"),
        [".bmp"] = (FileCategory.Image, "BMP 位图", "image/bmp"),
        [".gif"] = (FileCategory.Image, "GIF 图片", "image/gif"),

        // 文档
        [".pdf"] = (FileCategory.Document, "PDF 文档", "application/pdf"),
    };

    // 常见文件魔数签名
    private static readonly List<(byte[] Magic, int Offset, FileCategory Category, string DisplayName, string MimeType)> MagicSignatures = new()
    {
        // 图片
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, FileCategory.Image, "PNG 图片", "image/png"),
        (new byte[] { 0x42, 0x4D }, 0, FileCategory.Image, "BMP 位图", "image/bmp"),
        (new byte[] { 0xFF, 0xD8, 0xFF }, 0, FileCategory.Image, "JPEG 图片", "image/jpeg"),
        (new byte[] { 0x47, 0x49, 0x46, 0x38 }, 0, FileCategory.Image, "GIF 图片", "image/gif"),

        // 压缩/归档
        (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0, FileCategory.Archive, "ZIP 压缩包", "application/zip"),
        (new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }, 0, FileCategory.Archive, "RAR 压缩包", "application/vnd.rar"),
        (new byte[] { 0x1F, 0x8B, 0x08 }, 0, FileCategory.Archive, "GZip 压缩文件", "application/gzip"),

        // 可执行文件
        (new byte[] { 0x4D, 0x5A }, 0, FileCategory.Executable, "Windows 可执行文件", "application/x-msdownload"),
        (new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, 0, FileCategory.Executable, "ELF 可执行文件", "application/x-elf"),

        // 文档
        (new byte[] { 0x25, 0x50, 0x44, 0x46 }, 0, FileCategory.Document, "PDF 文档", "application/pdf"),

        // 其他
        (new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, 0, FileCategory.Binary, "Java Class 文件", "application/java-vm"),
    };

    public FileTypeInfo DetectByExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);

        if (string.IsNullOrEmpty(ext))
            return new FileTypeInfo(FileCategory.Unknown, "", "未知类型 (无扩展名)");

        if (ExtensionMap.TryGetValue(ext, out var info))
            return new FileTypeInfo(info.Category, ext, info.DisplayName,
                info.Category == FileCategory.Text, null, info.MimeType);

        return new FileTypeInfo(FileCategory.Binary, ext, $"二进制文件 ({ext})");
    }

    public FileTypeInfo DetectByHeader(byte[] headerBytes)
    {
        foreach (var (magic, offset, category, name, mime) in MagicSignatures)
        {
            if (offset + magic.Length > headerBytes.Length)
                continue;

            var match = true;
            for (int i = 0; i < magic.Length; i++)
            {
                if (headerBytes[offset + i] != magic[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return new FileTypeInfo(category, "", name, false, null, mime);
        }

        return new FileTypeInfo(FileCategory.Binary, "", "未知二进制文件");
    }

    public FileTypeInfo Detect(string filePath, byte[] headerBytes)
    {
        // 优先基于魔数匹配
        var headerResult = DetectByHeader(headerBytes);
        if (headerResult.Category != FileCategory.Unknown && headerResult.Category != FileCategory.Binary)
            return headerResult;

        // 其次基于扩展名判断
        var extResult = DetectByExtension(filePath);

        // 如果扩展名判断为文本类型，进一步确认（没有魔数匹配到就按扩展名走）
        if (extResult.IsText)
        {
            // 尝试检测是否真的是文本：检查前 N 字节是否都是可打印字符
            var encodingName = DetectTextEncodingName(headerBytes);
            return new FileTypeInfo(FileCategory.Text, extResult.Extension,
                extResult.DisplayName, true, encodingName, extResult.MimeType);
        }

        // 魔数命中但扩展名未知 → 用魔数结果
        if (headerResult.Category != FileCategory.Unknown)
            return headerResult;

        return extResult;
    }

    /// <summary>
    /// 检测文本编码名称：检查 BOM 头
    /// </summary>
    public static string DetectTextEncodingName(byte[] headerBytes)
    {
        if (headerBytes.Length >= 3 && headerBytes[0] == 0xEF && headerBytes[1] == 0xBB && headerBytes[2] == 0xBF)
            return "utf-8";

        if (headerBytes.Length >= 2 && headerBytes[0] == 0xFF && headerBytes[1] == 0xFE)
            return "utf-16LE";

        if (headerBytes.Length >= 2 && headerBytes[0] == 0xFE && headerBytes[1] == 0xFF)
            return "utf-16BE";

        // 默认 UTF-8
        return "utf-8";
    }
}
