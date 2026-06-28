namespace FileStruct.Services.StructureRecognition.SevenZipParser;

/// <summary>7z 头解析结果</summary>
public class SevenZipParseResult
{
    /// <summary>文件总数（从 FilesInfo.NumFiles 获取）</summary>
    public int NumFiles { get; set; }

    /// <summary>解析出的文件条目</summary>
    public List<SevenZipFileEntry> Files { get; set; } = new();

    /// <summary>是否检测到 7zAES 或其它加密</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>NextHeader 是否为编码头（压缩）</summary>
    public bool HeaderIsCompressed { get; set; }

    /// <summary>PackInfo 中解析的包裹流信息</summary>
    public List<PackStreamInfo> PackStreams { get; set; } = new();

    /// <summary>解析错误信息（非致命）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>检测到的压缩方法（多方法时以 + 连接）</summary>
    public string? CompressionMethods { get; set; }

    /// <summary>LZMA 编码器属性（5 字节，用于 EncodedHeader 解压）</summary>
    public byte[]? LzmaProperties { get; set; }

    /// <summary>编码头解压后大小（从 kCodersUnPackSize 获取）</summary>
    public int HeaderUnpackedSize { get; set; }

    /// <summary>NumFolders（从 CodersInfo 获取，供 SubStreamsInfo 使用）</summary>
    public int NumFolders { get; set; }

    /// <summary>SubStreamsInfo kSize 解压后大小列表（每个子流对应一个文件）</summary>
    public List<long> SubStreamUnpackSizes { get; set; } = new();
}

/// <summary>7z 压缩包中的单个文件条目</summary>
public class SevenZipFileEntry
{
    /// <summary>文件名（UTF-16LE 解码后的字符串）</summary>
    public string Name { get; set; } = "";

    /// <summary>解压后大小</summary>
    public long UnpackedSize { get; set; }

    /// <summary>压缩后大小</summary>
    public long PackedSize { get; set; }

    /// <summary>数据在文件中的偏移</summary>
    public long DataOffset { get; set; }

    /// <summary>压缩方法名称</summary>
    public string CompressionMethod { get; set; } = "";

    /// <summary>是否加密</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>是否为空流（文件夹/空文件）</summary>
    public bool IsEmptyStream { get; set; }

    /// <summary>所在分卷索引（多卷时有效）</summary>
    public int VolumeIndex { get; set; }

    /// <summary>文件修改时间（Unix 时间戳，0 表示未知）</summary>
    public long ModificationTime { get; set; }
}

/// <summary>7z 包裹流信息</summary>
public class PackStreamInfo
{
    /// <summary>相对容器起始的偏移</summary>
    public long PackPos { get; set; }

    /// <summary>压缩后大小</summary>
    public long PackSize { get; set; }

    /// <summary>所在分卷索引</summary>
    public int VolumeIndex { get; set; }

    /// <summary>是否检测到 CRC</summary>
    public bool HasCrc { get; set; }

    /// <summary>CRC32 值</summary>
    public uint Crc { get; set; }
}
