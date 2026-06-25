using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace FileStruct.Core.Models;

/// <summary>
/// 核心抽象：封装内存映射文件（MemoryMappedFile），提供零拷贝字节读取访问。
/// 这是整个应用程序的基础数据结构。
/// </summary>
public sealed class BinaryBuffer : IDisposable
{
    /// <summary>
    /// 默认最大文件大小：200 MB
    /// </summary>
    public const long DefaultMaxFileSize = 200 * 1024 * 1024;

    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _viewAccessor;
    private bool _isDisposed;

    private BinaryBuffer(string filePath, long length, MemoryMappedFile mappedFile,
        MemoryMappedViewAccessor viewAccessor)
    {
        FilePath = filePath;
        Length = length;
        _mappedFile = mappedFile;
        _viewAccessor = viewAccessor;
    }

    /// <summary>
    /// 原始文件路径
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 文件总长度（字节）
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// 文件名（不含路径）
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// 从文件路径创建 BinaryBuffer，使用 MemoryMappedFile 实现零拷贝加载。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="maxSize">允许的最大文件大小，默认 200MB</param>
    /// <returns>BinaryBuffer 实例</returns>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    /// <exception cref="FileTooLargeException">文件超过最大大小</exception>
    /// <exception cref="IOException">文件无法读取</exception>
    public static BinaryBuffer LoadFromFile(string filePath, long maxSize = DefaultMaxFileSize)
    {
        if (!File.Exists(filePath))
            throw new System.IO.FileNotFoundException($"文件未找到: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > maxSize)
            throw new Exceptions.FileTooLargeException(fileInfo.Length, maxSize);

        if (fileInfo.Length == 0)
            throw new Exceptions.FileLoadException($"文件为空: {filePath}", filePath);

        var mappedFile = MemoryMappedFile.CreateFromFile(
            filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        var viewAccessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        return new BinaryBuffer(filePath, fileInfo.Length, mappedFile, viewAccessor);
    }

    /// <summary>
    /// 读取单个字节
    /// </summary>
    public byte ReadByte(long offset)
    {
        CheckDisposed();
        ValidateOffset(offset, 1);
        return _viewAccessor.ReadByte(offset);
    }

    /// <summary>
    /// 批量读取字节到目标数组
    /// </summary>
    public int ReadBytes(long offset, byte[] destination, int index, int count)
    {
        CheckDisposed();
        ValidateOffset(offset, count);
        return _viewAccessor.ReadArray(offset, destination, index, count);
    }

    /// <summary>
    /// 读取指定范围的字节数组
    /// </summary>
    public byte[] ReadBytes(long offset, int count)
    {
        var buffer = new byte[count];
        ReadBytes(offset, buffer, 0, count);
        return buffer;
    }

    /// <summary>
    /// 读取 UInt16，支持大小端切换
    /// </summary>
    public ushort ReadUInt16(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[2];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(span)
                            : BinaryPrimitives.ReadUInt16BigEndian(span);
    }

    /// <summary>
    /// 读取 Int16
    /// </summary>
    public short ReadInt16(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[2];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadInt16LittleEndian(span)
                            : BinaryPrimitives.ReadInt16BigEndian(span);
    }

    /// <summary>
    /// 读取 UInt32
    /// </summary>
    public uint ReadUInt32(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[4];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(span)
                            : BinaryPrimitives.ReadUInt32BigEndian(span);
    }

    /// <summary>
    /// 读取 Int32
    /// </summary>
    public int ReadInt32(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[4];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(span)
                            : BinaryPrimitives.ReadInt32BigEndian(span);
    }

    /// <summary>
    /// 读取 UInt64
    /// </summary>
    public ulong ReadUInt64(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[8];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadUInt64LittleEndian(span)
                            : BinaryPrimitives.ReadUInt64BigEndian(span);
    }

    /// <summary>
    /// 读取 Int64
    /// </summary>
    public long ReadInt64(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[8];
        ReadSpan(offset, span);
        return littleEndian ? BinaryPrimitives.ReadInt64LittleEndian(span)
                            : BinaryPrimitives.ReadInt64BigEndian(span);
    }

    /// <summary>
    /// 读取 float
    /// </summary>
    public float ReadSingle(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[4];
        ReadSpan(offset, span);

        if (!littleEndian) span.Reverse();
        return BitConverter.ToSingle(span);
    }

    /// <summary>
    /// 读取 double
    /// </summary>
    public double ReadDouble(long offset, bool littleEndian = true)
    {
        Span<byte> span = stackalloc byte[8];
        ReadSpan(offset, span);

        if (!littleEndian) span.Reverse();
        return BitConverter.ToDouble(span);
    }

    /// <summary>
    /// 读取指定编码的字符串
    /// </summary>
    public string ReadString(long offset, int count, Encoding encoding)
    {
        var bytes = ReadBytes(offset, count);
        return encoding.GetString(bytes);
    }

    /// <summary>
    /// 验证偏移是否合法
    /// </summary>
    public bool IsValidOffset(long offset) => offset >= 0 && offset < Length;

    /// <summary>
    /// 验证范围是否合法
    /// </summary>
    public bool IsValidRange(long offset, int count) =>
        offset >= 0 && count > 0 && offset + count <= Length;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _viewAccessor?.Dispose();
        _mappedFile?.Dispose();
    }

    private void ReadSpan(long offset, Span<byte> destination)
    {
        ValidateOffset(offset, destination.Length);
        // MemoryMappedViewAccessor.ReadArray requires T[], so copy through a temporary buffer
        byte[] temp = new byte[destination.Length];
        _viewAccessor.ReadArray(offset, temp, 0, temp.Length);
        temp.CopyTo(destination);
    }

    private void CheckDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(BinaryBuffer));
    }

    private void ValidateOffset(long offset, int count)
    {
        if (!IsValidRange(offset, count))
            throw new ArgumentOutOfRangeException(nameof(offset),
                $"偏移量越界: offset={offset}, count={count}, fileLength={Length}");
    }
}
