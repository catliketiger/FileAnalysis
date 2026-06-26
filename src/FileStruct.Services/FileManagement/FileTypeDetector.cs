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
        // ═══ 文本格式（编程语言 + 脚本 + 配置）═══
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
        [".jsx"] = (FileCategory.Text, "JSX 文件", "text/javascript"),
        [".ts"] = (FileCategory.Text, "TypeScript 文件", "application/typescript"),
        [".tsx"] = (FileCategory.Text, "TSX 文件", "application/typescript"),
        [".vue"] = (FileCategory.Text, "Vue 组件", "text/html"),
        [".svelte"] = (FileCategory.Text, "Svelte 组件", "text/html"),
        [".py"] = (FileCategory.Text, "Python 脚本", "text/x-python"),
        [".java"] = (FileCategory.Text, "Java 源文件", "text/x-java"),
        [".kt"] = (FileCategory.Text, "Kotlin 源文件", "text/x-kotlin"),
        [".kts"] = (FileCategory.Text, "Kotlin 脚本", "text/x-kotlin"),
        [".groovy"] = (FileCategory.Text, "Groovy 脚本", "text/x-groovy"),
        [".scala"] = (FileCategory.Text, "Scala 源文件", "text/x-scala"),
        [".c"] = (FileCategory.Text, "C 源文件", "text/x-c"),
        [".h"] = (FileCategory.Text, "C/C++ 头文件", "text/x-c"),
        [".cpp"] = (FileCategory.Text, "C++ 源文件", "text/x-c++"),
        [".hpp"] = (FileCategory.Text, "C++ 头文件", "text/x-c++"),
        [".cc"] = (FileCategory.Text, "C++ 源文件", "text/x-c++"),
        [".cxx"] = (FileCategory.Text, "C++ 源文件", "text/x-c++"),
        [".hxx"] = (FileCategory.Text, "C++ 头文件", "text/x-c++"),
        [".cs"] = (FileCategory.Text, "C# 源文件", "text/x-csharp"),
        [".fs"] = (FileCategory.Text, "F# 源文件", "text/x-fsharp"),
        [".vb"] = (FileCategory.Text, "VB.NET 源文件", "text/x-vb"),
        [".swift"] = (FileCategory.Text, "Swift 源文件", "text/x-swift"),
        [".go"] = (FileCategory.Text, "Go 源文件", "text/x-go"),
        [".rs"] = (FileCategory.Text, "Rust 源文件", "text/x-rust"),
        [".rb"] = (FileCategory.Text, "Ruby 脚本", "text/x-ruby"),
        [".php"] = (FileCategory.Text, "PHP 脚本", "text/x-php"),
        [".pl"] = (FileCategory.Text, "Perl 脚本", "text/x-perl"),
        [".pm"] = (FileCategory.Text, "Perl 模块", "text/x-perl"),
        [".sh"] = (FileCategory.Text, "Shell 脚本", "application/x-sh"),
        [".bash"] = (FileCategory.Text, "Bash 脚本", "application/x-sh"),
        [".zsh"] = (FileCategory.Text, "Zsh 脚本", "application/x-sh"),
        [".ps1"] = (FileCategory.Text, "PowerShell 脚本", "application/x-powershell"),
        [".bat"] = (FileCategory.Text, "批处理文件", "application/bat"),
        [".cmd"] = (FileCategory.Text, "命令脚本", "text/plain"),
        [".lua"] = (FileCategory.Text, "Lua 脚本", "text/x-lua"),
        [".sql"] = (FileCategory.Text, "SQL 文件", "application/sql"),
        [".r"] = (FileCategory.Text, "R 脚本", "text/x-r"),
        [".dart"] = (FileCategory.Text, "Dart 源文件", "text/x-dart"),
        [".m"] = (FileCategory.Text, "Objective-C 源文件", "text/x-objcsrc"),
        [".mm"] = (FileCategory.Text, "Objective-C++ 源文件", "text/x-objcsrc"),
        [".ini"] = (FileCategory.Text, "INI 配置文件", "text/plain"),
        [".cfg"] = (FileCategory.Text, "配置文件", "text/plain"),
        [".log"] = (FileCategory.Text, "日志文件", "text/plain"),
        [".toml"] = (FileCategory.Text, "TOML 配置文件", "text/plain"),
        [".lock"] = (FileCategory.Text, "Lock 文件", "text/plain"),
        [".sln"] = (FileCategory.Text, "解决方案文件", "text/plain"),
        [".csproj"] = (FileCategory.Text, "C# 项目文件", "application/xml"),
        [".fsproj"] = (FileCategory.Text, "F# 项目文件", "application/xml"),
        [".vbproj"] = (FileCategory.Text, "VB 项目文件", "application/xml"),
        [".props"] = (FileCategory.Text, "MSBuild 属性文件", "application/xml"),
        [".targets"] = (FileCategory.Text, "MSBuild 目标文件", "application/xml"),
        [".makefile"] = (FileCategory.Text, "Makefile", "text/plain"),
        [".cmake"] = (FileCategory.Text, "CMake 文件", "text/plain"),
        [".gradle"] = (FileCategory.Text, "Gradle 脚本", "text/x-groovy"),

        // ═══ 可执行文件 ═══
        [".exe"] = (FileCategory.Executable, "Windows 可执行文件", "application/x-msdownload"),
        [".dll"] = (FileCategory.Executable, "动态链接库", "application/x-msdownload"),
        [".elf"] = (FileCategory.Executable, "ELF 可执行文件", "application/x-elf"),
        [".msi"] = (FileCategory.Executable, "Windows 安装包", "application/x-msi"),
        [".apk"] = (FileCategory.Executable, "Android 应用包", "application/vnd.android.package-archive"),
        [".dmg"] = (FileCategory.Executable, "macOS 磁盘映像", "application/x-apple-diskimage"),
        [".bin"] = (FileCategory.Executable, "二进制文件", "application/octet-stream"),
        [".appimage"] = (FileCategory.Executable, "AppImage 应用", "application/x-appimage"),
        [".deb"] = (FileCategory.Executable, "Debian 软件包", "application/vnd.debian.binary-package"),
        [".rpm"] = (FileCategory.Executable, "RPM 软件包", "application/x-rpm"),
        [".run"] = (FileCategory.Executable, "Linux 安装脚本", "application/x-executable"),
        [".lnk"] = (FileCategory.Binary, "Windows 快捷方式", "application/x-ms-shortcut"),

        // ═══ 压缩/归档 ═══
        [".zip"] = (FileCategory.Archive, "ZIP 压缩包", "application/zip"),
        [".rar"] = (FileCategory.Archive, "RAR 压缩包", "application/vnd.rar"),
        [".7z"] = (FileCategory.Archive, "7z 压缩包", "application/x-7z-compressed"),
        [".gz"] = (FileCategory.Archive, "GZip 压缩文件", "application/gzip"),
        [".tar"] = (FileCategory.Archive, "TAR 归档", "application/x-tar"),
        [".bz2"] = (FileCategory.Archive, "BZip2 压缩文件", "application/x-bzip2"),
        [".xz"] = (FileCategory.Archive, "XZ 压缩文件", "application/x-xz"),
        [".zst"] = (FileCategory.Archive, "Zstd 压缩文件", "application/zstd"),
        [".iso"] = (FileCategory.Archive, "光盘映像", "application/x-iso9660-image"),
        [".cab"] = (FileCategory.Archive, "Cabinet 压缩包", "application/vnd.ms-cab-compressed"),

        // ═══ 图片 ═══
        [".png"] = (FileCategory.Image, "PNG 图片", "image/png"),
        [".jpg"] = (FileCategory.Image, "JPEG 图片", "image/jpeg"),
        [".jpeg"] = (FileCategory.Image, "JPEG 图片", "image/jpeg"),
        [".bmp"] = (FileCategory.Image, "BMP 位图", "image/bmp"),
        [".gif"] = (FileCategory.Image, "GIF 图片", "image/gif"),
        [".webp"] = (FileCategory.Image, "WebP 图片", "image/webp"),
        [".svg"] = (FileCategory.Image, "SVG 矢量图", "image/svg+xml"),
        [".ico"] = (FileCategory.Image, "图标文件", "image/x-icon"),
        [".tiff"] = (FileCategory.Image, "TIFF 图片", "image/tiff"),
        [".tif"] = (FileCategory.Image, "TIFF 图片", "image/tiff"),
        [".psd"] = (FileCategory.Image, "PSD 文件", "image/vnd.adobe.photoshop"),
        [".raw"] = (FileCategory.Image, "RAW 图片", "image/x-raw"),

        // ═══ 音频 ═══
        [".mp3"] = (FileCategory.Audio, "MP3 音频", "audio/mpeg"),
        [".wav"] = (FileCategory.Audio, "WAV 音频", "audio/wav"),
        [".flac"] = (FileCategory.Audio, "FLAC 音频", "audio/flac"),
        [".aac"] = (FileCategory.Audio, "AAC 音频", "audio/aac"),
        [".ogg"] = (FileCategory.Audio, "OGG 音频", "audio/ogg"),
        [".wma"] = (FileCategory.Audio, "WMA 音频", "audio/x-ms-wma"),
        [".m4a"] = (FileCategory.Audio, "M4A 音频", "audio/mp4"),
        [".opus"] = (FileCategory.Audio, "Opus 音频", "audio/opus"),
        [".mid"] = (FileCategory.Audio, "MIDI 文件", "audio/midi"),
        [".midi"] = (FileCategory.Audio, "MIDI 文件", "audio/midi"),
        [".ape"] = (FileCategory.Audio, "APE 音频", "audio/x-ape"),
        [".aiff"] = (FileCategory.Audio, "AIFF 音频", "audio/aiff"),

        // ═══ 视频 ═══
        [".mp4"] = (FileCategory.Video, "MP4 视频", "video/mp4"),
        [".avi"] = (FileCategory.Video, "AVI 视频", "video/x-msvideo"),
        [".mkv"] = (FileCategory.Video, "MKV 视频", "video/x-matroska"),
        [".mov"] = (FileCategory.Video, "MOV 视频", "video/quicktime"),
        [".wmv"] = (FileCategory.Video, "WMV 视频", "video/x-ms-wmv"),
        [".flv"] = (FileCategory.Video, "FLV 视频", "video/x-flv"),
        [".webm"] = (FileCategory.Video, "WebM 视频", "video/webm"),
        [".m4v"] = (FileCategory.Video, "M4V 视频", "video/x-m4v"),
        [".mpg"] = (FileCategory.Video, "MPEG 视频", "video/mpeg"),
        [".mpeg"] = (FileCategory.Video, "MPEG 视频", "video/mpeg"),
        [".3gp"] = (FileCategory.Video, "3GP 视频", "video/3gpp"),
        // [".ts"] 已归入 TypeScript（文字类）

        // ═══ 文档 ═══
        [".pdf"] = (FileCategory.Document, "PDF 文档", "application/pdf"),
        [".doc"] = (FileCategory.Document, "Word 文档", "application/msword"),
        [".docx"] = (FileCategory.Document, "Word 文档", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        [".xls"] = (FileCategory.Document, "Excel 表格", "application/vnd.ms-excel"),
        [".xlsx"] = (FileCategory.Document, "Excel 表格", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        [".ppt"] = (FileCategory.Document, "PowerPoint 演示", "application/vnd.ms-powerpoint"),
        [".pptx"] = (FileCategory.Document, "PowerPoint 演示", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        [".wps"] = (FileCategory.Document, "WPS 文档", "application/vnd.ms-works"),
        [".odt"] = (FileCategory.Document, "OpenDocument 文本", "application/vnd.oasis.opendocument.text"),
        [".ods"] = (FileCategory.Document, "OpenDocument 表格", "application/vnd.oasis.opendocument.spreadsheet"),
        [".odp"] = (FileCategory.Document, "OpenDocument 演示", "application/vnd.oasis.opendocument.presentation"),
        [".rtf"] = (FileCategory.Document, "RTF 文档", "application/rtf"),

        // ═══ CAD 专用 ═══
        [".dwg"] = (FileCategory.Binary, "AutoCAD 图纸", "application/acad"),
        [".dxf"] = (FileCategory.Binary, "DXF 交换文件", "image/vnd.dxf"),
        [".dgn"] = (FileCategory.Binary, "MicroStation 设计", "application/x-dgn"),
        [".stp"] = (FileCategory.Binary, "STEP 3D 模型", "application/step"),
        [".step"] = (FileCategory.Binary, "STEP 3D 模型", "application/step"),
        [".iges"] = (FileCategory.Binary, "IGES 模型", "application/iges"),
        [".igs"] = (FileCategory.Binary, "IGES 模型", "application/iges"),
        [".sat"] = (FileCategory.Binary, "SAT 3D 模型", "application/x-sat"),
        [".3dm"] = (FileCategory.Binary, "Rhinoceros 3D", "application/x-rhinoceros"),
        [".skp"] = (FileCategory.Binary, "SketchUp 模型", "application/x-sketchup"),
        [".c4d"] = (FileCategory.Binary, "Cinema 4D", "application/x-cinema4d"),
        [".blend"] = (FileCategory.Binary, "Blender 文件", "application/x-blender"),
        [".max"] = (FileCategory.Binary, "3ds Max 场景", "application/x-3ds"),
        [".fcstd"] = (FileCategory.Binary, "FreeCAD 文档", "application/x-freecad"),

        // ═══ 字体文件 ═══
        [".ttf"] = (FileCategory.Binary, "TrueType 字体", "font/ttf"),
        [".otf"] = (FileCategory.Binary, "OpenType 字体", "font/otf"),
        [".woff"] = (FileCategory.Binary, "WOFF 字体", "font/woff"),
        [".woff2"] = (FileCategory.Binary, "WOFF2 字体", "font/woff2"),
        [".eot"] = (FileCategory.Binary, "Embedded OpenType 字体", "application/vnd.ms-fontobject"),
        [".fon"] = (FileCategory.Binary, "FON 字体", "application/x-font"),

        // ═══ 虚拟机/容器映像 ═══
        [".vmdk"] = (FileCategory.Binary, "VMware 虚拟磁盘", "application/x-vmdk"),
        [".vhd"] = (FileCategory.Binary, "Hyper-V 虚拟磁盘", "application/x-vhd"),
        [".vhdx"] = (FileCategory.Binary, "Hyper-V 虚拟磁盘", "application/x-vhd"),
        [".vdi"] = (FileCategory.Binary, "VirtualBox 虚拟磁盘", "application/x-vdi"),
        [".ova"] = (FileCategory.Binary, "OVA 虚拟设备", "application/x-ova"),
        [".ovf"] = (FileCategory.Binary, "OVF 虚拟设备", "application/x-ovf"),
        [".qcow2"] = (FileCategory.Binary, "QEMU 磁盘映像", "application/x-qemu-disk"),
        [".img"] = (FileCategory.Binary, "磁盘映像", "application/octet-stream"),
        [".dsk"] = (FileCategory.Binary, "磁盘映像", "application/octet-stream"),
        [".hdd"] = (FileCategory.Binary, "虚拟硬盘", "application/octet-stream"),
    };

    // 常见文件魔数签名
    private static readonly List<(byte[] Magic, int Offset, FileCategory Category, string DisplayName, string MimeType)> MagicSignatures = new()
    {
        // ═══ 图片 ═══
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0, FileCategory.Image, "PNG 图片", "image/png"),
        ([0x42, 0x4D], 0, FileCategory.Image, "BMP 位图", "image/bmp"),
        ([0xFF, 0xD8, 0xFF], 0, FileCategory.Image, "JPEG 图片", "image/jpeg"),
        ([0x47, 0x49, 0x46, 0x38], 0, FileCategory.Image, "GIF 图片", "image/gif"),
        ([0x49, 0x49, 0x2A, 0x00], 0, FileCategory.Image, "TIFF 图片", "image/tiff"),
        ([0x4D, 0x4D, 0x00, 0x2A], 0, FileCategory.Image, "TIFF 图片", "image/tiff"),
        ([0x38, 0x42, 0x50, 0x53], 0, FileCategory.Image, "PSD 文件", "image/vnd.adobe.photoshop"),
        ([0x00, 0x00, 0x01, 0x00], 0, FileCategory.Image, "ICO 图标", "image/x-icon"),

        // ═══ 音频 ═══
        ([0x49, 0x44, 0x33], 0, FileCategory.Audio, "MP3 音频", "audio/mpeg"),
        ([0x66, 0x4C, 0x61, 0x43], 0, FileCategory.Audio, "FLAC 音频", "audio/flac"),
        ([0x4F, 0x67, 0x67, 0x53], 0, FileCategory.Audio, "OGG 音频", "audio/ogg"),
        ([0x4D, 0x54, 0x68, 0x64], 0, FileCategory.Audio, "MIDI 文件", "audio/midi"),

        // ═══ 视频 ═══
        ([0x41, 0x56, 0x49, 0x20], 8, FileCategory.Video, "AVI 视频", "video/x-msvideo"),
        ([0x66, 0x74, 0x79, 0x70], 4, FileCategory.Video, "MP4 视频", "video/mp4"),
        ([0x1A, 0x45, 0xDF, 0xA3], 0, FileCategory.Video, "MKV/WebM 视频", "video/x-matroska"),
        ([0x46, 0x4C, 0x56], 0, FileCategory.Video, "FLV 视频", "video/x-flv"),

        // ═══ 压缩/归档 ═══
        ([0x50, 0x4B, 0x03, 0x04], 0, FileCategory.Archive, "ZIP 压缩包", "application/zip"),
        ([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07], 0, FileCategory.Archive, "RAR 压缩包", "application/vnd.rar"),
        ([0x1F, 0x8B, 0x08], 0, FileCategory.Archive, "GZip 压缩文件", "application/gzip"),
        ([0x42, 0x5A, 0x68], 0, FileCategory.Archive, "BZip2 压缩文件", "application/x-bzip2"),
        ([0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00], 0, FileCategory.Archive, "XZ 压缩文件", "application/x-xz"),
        ([0x28, 0xB5, 0x2F, 0xFD], 0, FileCategory.Archive, "Zstd 压缩文件", "application/zstd"),
        ([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], 0, FileCategory.Archive, "7z 压缩包", "application/x-7z-compressed"),
        ([0x49, 0x53, 0x63, 0x28], 0, FileCategory.Archive, "CAB 压缩包", "application/vnd.ms-cab-compressed"),

        // ═══ 可执行文件 ═══
        ([0x4D, 0x5A], 0, FileCategory.Executable, "Windows 可执行文件", "application/x-msdownload"),
        ([0x7F, 0x45, 0x4C, 0x46], 0, FileCategory.Executable, "ELF 可执行文件", "application/x-elf"),
        ([0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E], 0, FileCategory.Executable, "Debian 软件包", "application/vnd.debian.binary-package"),
        ([0xED, 0xAB, 0xEE, 0xDB], 0, FileCategory.Executable, "RPM 软件包", "application/x-rpm"),

        // ═══ 文档 ═══
        ([0x25, 0x50, 0x44, 0x46], 0, FileCategory.Document, "PDF 文档", "application/pdf"),
        ([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], 0, FileCategory.Document, "OLE2 文档 (DOC/XLS/PPT)", "application/msword"),
        ([0x7B, 0x5C, 0x72, 0x74, 0x66], 0, FileCategory.Document, "RTF 文档", "application/rtf"),

        // ═══ 字体 ═══
        ([0x00, 0x01, 0x00, 0x00, 0x00], 0, FileCategory.Binary, "TrueType 字体", "font/ttf"),
        ([0x4F, 0x54, 0x54, 0x4F], 0, FileCategory.Binary, "OpenType 字体", "font/otf"),
        ([0x77, 0x4F, 0x46, 0x46], 0, FileCategory.Binary, "WOFF 字体", "font/woff"),
        ([0x77, 0x4F, 0x46, 0x32], 0, FileCategory.Binary, "WOFF2 字体", "font/woff2"),

        // ═══ 虚拟机/磁盘映像 ═══
        ([0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78], 0, FileCategory.Binary, "VHD 虚拟磁盘", "application/x-vhd"),
        ([0x76, 0x68, 0x64, 0x78, 0x66, 0x69, 0x6C, 0x65], 0, FileCategory.Binary, "VHDX 虚拟磁盘", "application/x-vhd"),
        ([0x51, 0x46, 0x49, 0xFB], 0, FileCategory.Binary, "QCOW2 磁盘映像", "application/x-qemu-disk"),
        ([0x43, 0x44, 0x30, 0x30, 0x31], 0x8001, FileCategory.Binary, "ISO 9660 光盘映像", "application/x-iso9660-image"),

        // 快捷方式
        ([0x4C, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00], 0, FileCategory.Binary, "Windows 快捷方式", "application/x-ms-shortcut"),

        // 其他
        ([0xCA, 0xFE, 0xBA, 0xBE], 0, FileCategory.Binary, "Java Class 文件", "application/java-vm"),
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
