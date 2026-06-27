using System.IO;
using System.Text.RegularExpressions;

namespace FileStruct.App.Utils;

/// <summary>分卷发现与完整性检查</summary>
public static class VolumeHelper
{
    public class VolumeInfo
    {
        public string BaseName { get; set; } = "";
        public List<string> Volumes { get; set; } = new();
        public List<string> MissingVolumes { get; set; } = new();
        public bool IsMultiVolume => Volumes.Count > 1;
    }

    /// <summary>扫描目录发现分卷集</summary>
    public static VolumeInfo DiscoverVolumes(string filePath)
    {
        var info = new VolumeInfo();
        var dir = Path.GetDirectoryName(filePath) ?? ".";
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        // ZIP 分卷: .z01 = 第1卷, .z02 = 第2卷, .zip = 末卷
        if (fileName.EndsWith(".zip") || Regex.IsMatch(fileName, @"\.z\d{2}$"))
        {
            var baseName = fileName.EndsWith(".zip")
                ? Path.GetFileNameWithoutExtension(filePath)
                : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath));
            info.BaseName = baseName ?? "";
            // .zNN 按序号排列在前
            for (int i = 1; i <= 999; i++)
            {
                var v = Path.Combine(dir, $"{baseName}.z{i:D2}");
                if (File.Exists(v)) info.Volumes.Add(v);
                else { break; }
            }
            // .zip 是最后一卷（中央目录所在）
            var baseZip = Path.Combine(dir, baseName + ".zip");
            if (File.Exists(baseZip) && !info.Volumes.Contains(baseZip))
                info.Volumes.Add(baseZip);
        }
        // RAR .partN.rar
        else if (fileName.Contains(".part") && fileName.EndsWith(".rar"))
        {
            info.BaseName = Path.GetFileNameWithoutExtension(filePath) ?? "";
            for (int i = 1; i <= 999; i++)
            {
                var v = Path.Combine(dir, $"part{i}.rar");
                if (File.Exists(v)) info.Volumes.Add(v); else break;
            }
        }
        // RAR .r00 .r01
        else if (Regex.IsMatch(fileName, @"\.r\d{2}$"))
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            info.BaseName = baseName ?? "";
            var baseRar = Path.Combine(dir, baseName + ".rar");
            if (File.Exists(baseRar)) info.Volumes.Add(baseRar);
            for (int i = 0; i <= 999; i++)
            {
                var v = Path.Combine(dir, $"{baseName}.r{i:D2}");
                if (File.Exists(v) && !info.Volumes.Contains(v)) info.Volumes.Add(v);
                else break;
            }
        }
        // 7z 分卷: .7z.NNN
        else if (fileName.Contains(".7z"))
        {
            var baseName = Regex.IsMatch(fileName, @"\.7z\.\d{3}$")
                ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath))
                : Path.GetFileNameWithoutExtension(filePath);
            info.BaseName = baseName ?? "";
            var base7z = Path.Combine(dir, baseName + ".7z");
            if (File.Exists(base7z)) info.Volumes.Add(base7z);
            for (int i = 1; i <= 999; i++)
            {
                var v = Path.Combine(dir, $"{baseName}.7z.{i:D3}");
                if (File.Exists(v)) info.Volumes.Add(v); else break;
            }
        }

        info.Volumes = info.Volumes.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        return info;
    }

    public static string GetVolumeSummary(VolumeInfo info)
    {
        if (!info.IsMultiVolume) return "";
        var missing = info.MissingVolumes.Count > 0 ? $"，缺少 {info.MissingVolumes.Count} 卷" : "";
        return $"分卷压缩包，共 {info.Volumes.Count} 卷{missing}";
    }
}
