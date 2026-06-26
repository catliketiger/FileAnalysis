using System.Security.Cryptography;
using System.Text.Json;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.ProjectManagement;

/// <summary>
/// 项目文件序列化器：JSON 格式，包含 SHA256 哈希校验
/// </summary>
public class ProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// 将项目数据序列化为 JSON 字符串
    /// </summary>
    public string Serialize(ProjectFile project)
    {
        project.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(project, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为项目数据
    /// </summary>
    public ProjectFile Deserialize(string json)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var project = JsonSerializer.Deserialize<ProjectFile>(json, JsonOptions);
            System.Diagnostics.Debug.WriteLine($"[ProjectSerializer] 反序列化耗时: {sw.ElapsedMilliseconds}ms");
            if (project == null)
                throw new InvalidDataException("项目文件解析失败：反序列化返回 null");
            return project;
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException($"项目文件 JSON 解析错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 计算文件的 SHA256 哈希值
    /// </summary>
    public async Task<string> ComputeHashAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }
}
