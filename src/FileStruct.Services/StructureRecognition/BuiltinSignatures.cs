using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public static class BuiltinSignatures
{
    public static List<SignatureDefinition> GetAll() => new()
    {
        // 图片格式
        new("PNG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            description: "PNG 图片"),
        new("BMP", [0x42, 0x4D], description: "BMP 位图"),
        new("JPEG", [0xFF, 0xD8, 0xFF], description: "JPEG 图片"),
        new("GIF", [0x47, 0x49, 0x46, 0x38], description: "GIF 图片"),

        // 压缩/归档
        new("ZIP", [0x50, 0x4B, 0x03, 0x04], description: "ZIP 压缩包"),
        new("RAR", [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07], description: "RAR 压缩包"),
        new("GZip", [0x1F, 0x8B, 0x08], description: "GZip 压缩文件"),

        // 可执行文件
        new("PE", [0x4D, 0x5A], description: "Windows PE 可执行文件"),
        new("ELF", [0x7F, 0x45, 0x4C, 0x46], description: "ELF 可执行文件"),

        // 文档
        new("PDF", [0x25, 0x50, 0x44, 0x46], description: "PDF 文档"),

        // 其他
        new("Java Class", [0xCA, 0xFE, 0xBA, 0xBE], description: "Java Class 文件"),
        new("Mach-O", [0xFE, 0xED, 0xFA, 0xCE], description: "Mach-O 可执行文件 (32-bit BE)"),
        new("Mach-O", [0xFE, 0xED, 0xFA, 0xCF], description: "Mach-O 可执行文件 (64-bit BE)"),
        new("RIFF", [0x52, 0x49, 0x46, 0x46], description: "RIFF 格式 (AVI/WAV)"),
    };
}
