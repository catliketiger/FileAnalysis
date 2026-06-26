using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.FileManagement;

/// <summary>
/// 文件管理服务：负责文件加载、类型检测
/// </summary>
public class FileService : IFileService
{
    private readonly IFileTypeDetector _typeDetector;
    private readonly ILogService _logger;

    public FileService(IFileTypeDetector typeDetector, ILogService logger)
    {
        _typeDetector = typeDetector;
        _logger = logger;
    }

    public async Task<BinaryBuffer> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.Debug($"开始加载文件: {filePath}");

        // 后台线程执行文件加载（MMF创建+类型检测），不阻塞 UI
        var buffer = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buf = BinaryBuffer.LoadFromFile(filePath);
            var fileType = DetectFileType(buf);
            _logger.Info($"文件已加载: {buf.FileName}, 大小: {buf.Length:N0} 字节, 类型: {fileType.DisplayName}");
            return buf;
        }, cancellationToken).ConfigureAwait(false);

        return buffer;
    }

    public FileTypeInfo DetectFileType(string filePath)
    {
        byte[] header = new byte[16];
        try
        {
            using var stream = File.OpenRead(filePath);
            stream.ReadExactly(header, 0, Math.Min(16, (int)stream.Length));
        }
        catch
        {
            // 如果读头部失败，只基于扩展名判断
        }

        return _typeDetector.Detect(filePath, header);
    }

    public FileTypeInfo DetectFileType(BinaryBuffer buffer)
    {
        var header = buffer.ReadBytes(0, (int)Math.Min(65536, buffer.Length));
        return _typeDetector.Detect(buffer.FilePath, header);
    }

    public void CloseFile(BinaryBuffer buffer)
    {
        _logger.Debug($"关闭文件: {buffer.FileName}");
        buffer.Dispose();
    }
}
