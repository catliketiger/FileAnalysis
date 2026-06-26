using FileStruct.Core.Models;

namespace FileStruct.Infrastructure.Configuration;

/// <summary>
/// 内置规则提供器：以代码方式定义预置文件结构规则，避免嵌入式资源加载问题
/// </summary>
public static class BuiltinRuleProvider
{
    public static List<FormatRule> GetAll() =>
    [
        BmpRule(),
        GifRule(),
        GzipRule(),
        PngRule(),
        RiffRule(),
        PeRule(),
        ElfRule(),
        ZipRule(),
        LnkRule(),
        ClassRule(),
        MidiRule(),
        FlacRule(),
        Bzip2Rule(),
        TtfRule(),
        OtfRule(),
        Id3v2Rule(),
        TiffRule(),
        TiffBeRule(),
        IcoRule(),
        MinidumpRule(),
        SqliteRule(),
        TsRule(),
        IsoRule(),
        RtfRule(),
        WoffRule(),
        Woff2Rule(),
        Macho32Rule(),
        Macho64Rule(),
        AiffRule(),
        DebRule(),
        Ole2Rule(),
        RarRule(),
        PsdRule(),
        PdfRule(),
        OggRule(),
        Mp4Rule(),
    ];

    private static FormatRule CreateRule(string format, string desc,
        (byte[] magic, int offset, int minSize)[] signatures,
        (string name, (string field, string type, int offset, int len, string? endian)[] fields, bool seq)[] structures)
    {
        var rule = new FormatRule { Format = format, Description = desc, SourcePath = "builtin" };
        foreach (var (magic, offset, minSize) in signatures)
            rule.Signatures.Add(new FormatSignature { Name = $"{format} Magic", Pattern = magic, Offset = offset, MinFileSize = minSize });
        foreach (var (name, fields, seq) in structures)
        {
            var s = new FormatStructure { Name = name, Type = "struct", Sequential = seq };
            foreach (var (field, type, off, len, endian) in fields)
                s.Fields.Add(new FormatField { Name = field, Type = type, Offset = off, Length = len, Endianness = endian });
            rule.Structures.Add(s);
        }
        return rule;
    }

    private static FormatRule BmpRule() => CreateRule("BMP", "BMP 位图文件结构",
        [([0x42, 0x4D], 0, 26)],
        [
            ("BITMAPFILEHEADER", [
                ("bfType", "uint16", 0, 2, "LittleEndian"),
                ("bfSize", "uint32", 2, 4, "LittleEndian"),
                ("bfReserved1", "uint16", 6, 2, null),
                ("bfReserved2", "uint16", 8, 2, null),
                ("bfOffBits", "uint32", 10, 4, "LittleEndian"),
            ], false),
            ("BITMAPINFOHEADER", [
                ("biSize", "uint32", 14, 4, null),
                ("biWidth", "int32", 18, 4, null),
                ("biHeight", "int32", 22, 4, null),
                ("biPlanes", "uint16", 26, 2, null),
                ("biBitCount", "uint16", 28, 2, null),
                ("biCompression", "uint32", 30, 4, null),
                ("biSizeImage", "uint32", 34, 4, null),
                ("biXPelsPerMeter", "int32", 38, 4, null),
                ("biYPelsPerMeter", "int32", 42, 4, null),
                ("biClrUsed", "uint32", 46, 4, null),
                ("biClrImportant", "uint32", 50, 4, null),
            ], false),
        ]);

    private static FormatRule GifRule() => CreateRule("GIF", "GIF 图片文件结构",
        [([0x47, 0x49, 0x46, 0x38, 0x39, 0x61], 0, 14), ([0x47, 0x49, 0x46, 0x38, 0x37, 0x61], 0, 14)],
        [
            ("GIF Header", [
                ("Signature", "ascii", 0, 3, null),
                ("Version", "ascii", 3, 3, null),
                ("Width", "uint16", 6, 2, null),
                ("Height", "uint16", 8, 2, null),
                ("PackedField", "uint8", 10, 1, null),
                ("BgColorIndex", "uint8", 11, 1, null),
                ("PixelAspect", "uint8", 12, 1, null),
            ], false),
        ]);

    private static FormatRule GzipRule() => CreateRule("GZip", "GZip 压缩文件结构",
        [([0x1F, 0x8B, 0x08], 0, 18)],
        [
            ("GZip Header", [
                ("ID1", "uint8", 0, 1, null),
                ("ID2", "uint8", 1, 1, null),
                ("CM", "uint8", 2, 1, null),
                ("FLG", "uint8", 3, 1, null),
                ("MTIME", "uint32", 4, 4, null),
                ("XFL", "uint8", 8, 1, null),
                ("OS", "uint8", 9, 1, null),
            ], false),
        ]);

    private static FormatRule PngRule() => CreateRule("PNG", "PNG 图片文件结构",
        [([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0, 29)],
        [
            ("PNG Signature", [("Signature", "bytes", 0, 8, null)], false),
            ("IHDR Chunk", [
                ("DataLength", "uint32", 8, 4, null),
                ("ChunkType", "ascii", -1, 4, null),
                ("Width", "uint32", -1, 4, null),
                ("Height", "uint32", -1, 4, null),
                ("BitDepth", "uint8", -1, 1, null),
                ("ColorType", "uint8", -1, 1, null),
                ("Compression", "uint8", -1, 1, null),
                ("Filter", "uint8", -1, 1, null),
                ("Interlace", "uint8", -1, 1, null),
            ], true),
        ]);

    private static FormatRule RiffRule() => CreateRule("RIFF", "RIFF 容器格式 (WAV/AVI)",
        [([0x52, 0x49, 0x46, 0x46], 0, 12)],
        [
            ("RIFF Header", [
                ("ChunkID", "ascii", 0, 4, null),
                ("ChunkSize", "uint32", 4, 4, null),
                ("Format", "ascii", 8, 4, null),
            ], false),
            ("WAVE fmt", [
                ("SubChunk1ID", "ascii", 12, 4, null),
                ("SubChunk1Size", "uint32", -1, 4, null),
                ("AudioFormat", "uint16", -1, 2, null),
                ("NumChannels", "uint16", -1, 2, null),
                ("SampleRate", "uint32", -1, 4, null),
                ("ByteRate", "uint32", -1, 4, null),
                ("BlockAlign", "uint16", -1, 2, null),
                ("BitsPerSample", "uint16", -1, 2, null),
            ], true),
            ("WAVE data (PCM 标准位置)", [
                ("SubChunk2ID", "ascii", 36, 4, null),
                ("SubChunk2Size", "uint32", 40, 4, null),
            ], false),
        ]);

    private static FormatRule PeRule() => CreateRule("PE", "Windows PE 可执行文件结构",
        [([0x4D, 0x5A], 0, 64)],
        [
            ("DOS Header", [
                ("e_magic", "uint16", 0, 2, null),
                ("e_cblp", "uint16", 2, 2, null),
                ("e_cp", "uint16", 4, 2, null),
                ("e_crlc", "uint16", 6, 2, null),
                ("e_cparhdr", "uint16", 8, 2, null),
                ("e_minalloc", "uint16", 10, 2, null),
                ("e_maxalloc", "uint16", 12, 2, null),
                ("e_ss", "uint16", 14, 2, null),
                ("e_sp", "uint16", 16, 2, null),
                ("e_csum", "uint16", 18, 2, null),
                ("e_ip", "uint16", 20, 2, null),
                ("e_cs", "uint16", 22, 2, null),
                ("e_lfarlc", "uint16", 24, 2, null),
                ("e_ovno", "uint16", 26, 2, null),
                ("e_oemid", "uint16", 28, 2, null),
                ("e_oeminfo", "uint16", 30, 2, null),
                ("e_lfanew", "uint32", 60, 4, null),
            ], false),
            ("PE Signature", [
                ("PEMagic", "bytes", -4, 4, null),  // at e_lfanew = baseOffset - 4
            ], false),
            ("COFF File Header", [
                ("Machine", "uint16", 0, 2, null),
                ("NumberOfSections", "uint16", 2, 2, null),
                ("TimeDateStamp", "uint32", 4, 4, null),
                ("PointerToSymbolTable", "uint32", 8, 4, null),
                ("NumberOfSymbols", "uint32", 12, 4, null),
                ("SizeOfOptionalHeader", "uint16", 16, 2, null),
                ("Characteristics", "uint16", 18, 2, null),
            ], false),
            ("Optional Header (Standard)",
                [("Magic", "uint16", 20, 2, null),
                ("LinkerVersion", "uint16", 22, 2, null),
                ("SizeOfCode", "uint32", 24, 4, null),
                ("SizeOfInitializedData", "uint32", 28, 4, null),
                ("SizeOfUninitializedData", "uint32", 32, 4, null),
                ("AddressOfEntryPoint", "uint32", 36, 4, null),
                ("BaseOfCode", "uint32", 40, 4, null),
            ], false),
        ]);

    private static FormatRule ElfRule() => CreateRule("ELF", "ELF 可执行与链接格式",
        [([0x7F, 0x45, 0x4C, 0x46], 0, 52)],
        [
            ("ELF Header", [
                ("e_ident_magic", "bytes", 0, 4, null),
                ("e_ident_class", "uint8", 4, 1, null),
                ("e_ident_data", "uint8", 5, 1, null),
                ("e_ident_version", "uint8", 6, 1, null),
                ("e_ident_osabi", "uint8", 7, 1, null),
                ("e_ident_abiversion", "uint8", 8, 1, null),
                ("e_type", "uint16", 16, 2, null),
                ("e_machine", "uint16", 18, 2, null),
                ("e_version", "uint32", 20, 4, null),
                ("e_entry", "uint64", 24, 8, null),
                ("e_phoff", "uint64", 32, 8, null),
                ("e_shoff", "uint64", 40, 8, null),
                ("e_flags", "uint32", 48, 4, null),
                ("e_ehsize", "uint16", 52, 2, null),
                ("e_phentsize", "uint16", 54, 2, null),
                ("e_phnum", "uint16", 56, 2, null),
                ("e_shentsize", "uint16", 58, 2, null),
                ("e_shnum", "uint16", 60, 2, null),
                ("e_shstrndx", "uint16", 62, 2, null),
            ], false),
        ]);

    private static FormatRule ZipRule() => CreateRule("ZIP", "ZIP 压缩包结构",
        [([0x50, 0x4B, 0x03, 0x04], 0, 30)],
        [
            ("ZIP Local File Header", [
                ("Signature", "uint32", 0, 4, null),
                ("VersionNeeded", "uint16", 4, 2, null),
                ("Flags", "uint16", 6, 2, null),
                ("Compression", "uint16", 8, 2, null),
                ("ModTime", "uint16", 10, 2, null),
                ("ModDate", "uint16", 12, 2, null),
                ("CRC32", "uint32", 14, 4, null),
                ("CompressedSize", "uint32", 18, 4, null),
                ("UncompressedSize", "uint32", 22, 4, null),
                ("FileNameLen", "uint16", 26, 2, null),
                ("ExtraLen", "uint16", 28, 2, null),
            ], false),
        ]);

    private static FormatRule LnkRule() => CreateRule("LNK", "Windows 快捷方式文件结构",
        [([0x4C, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00], 0, 76)],
        [
            ("Shell Link Header", [
                ("HeaderSize", "uint32", 0, 4, null),
                ("LinkCLSID", "bytes", 4, 16, null),
                ("LinkFlags", "uint32", 20, 4, null),
                ("FileAttributes", "uint32", 24, 4, null),
                ("CreationTime", "uint64", 28, 8, null),
                ("AccessTime", "uint64", 36, 8, null),
                ("WriteTime", "uint64", 44, 8, null),
                ("FileSize", "uint32", 52, 4, null),
                ("IconIndex", "int32", 56, 4, null),
                ("ShowCommand", "uint32", 60, 4, null),
                ("Hotkey", "uint16", 64, 2, null),
            ], false),
        ]);

    private static FormatRule ClassRule() => CreateRule("Java Class", "Java Class 文件结构",
        [([0xCA, 0xFE, 0xBA, 0xBE], 0, 28)],
        [
            ("Class File Header", [
                ("Magic", "uint32", 0, 4, "BigEndian"),
                ("MinorVersion", "uint16", 4, 2, "BigEndian"),
                ("MajorVersion", "uint16", 6, 2, "BigEndian"),
                ("ConstantPoolCount", "uint16", 8, 2, "BigEndian"),
            ], false),
        ]);

    private static FormatRule MidiRule() => CreateRule("MIDI", "MIDI 文件结构",
        [([0x4D, 0x54, 0x68, 0x64], 0, 14)],
        [
            ("MIDI Header Chunk", [
                ("ChunkID", "ascii", 0, 4, null),
                ("ChunkSize", "uint32", 4, 4, null),
                ("FormatType", "uint16", 8, 2, null),
                ("NumTracks", "uint16", 10, 2, null),
                ("TimeDivision", "uint16", 12, 2, null),
            ], false),
        ]);

    private static FormatRule FlacRule() => CreateRule("FLAC", "FLAC 音频文件结构",
        [([0x66, 0x4C, 0x61, 0x43], 0, 42)],
        [
            ("FLAC Marker", [("Marker", "ascii", 0, 4, null)], false),
            ("STREAMINFO Block Header", [
                ("MetaBlockHeader", "bytes", 4, 4, null),
            ], false),
            ("STREAMINFO", [
                ("MinBlockSize", "uint16", 8, 2, null),
                ("MaxBlockSize", "uint16", 10, 2, null),
                ("MinFrameSize", "uint32", 12, 3, null),
                ("MaxFrameSize", "uint32", 15, 3, null),
                ("SampleRate", "uint32", 20, 2, null),
                ("NumChannels", "uint8", 22, 1, null),
                ("BitsPerSample", "uint8", 23, 1, null),
                ("TotalSamples", "uint64", 24, 8, null),
            ], false),
        ]);

    private static FormatRule Bzip2Rule() => CreateRule("BZip2", "BZip2 压缩文件结构",
        [([0x42, 0x5A, 0x68], 0, 14)],
        [
            ("BZip2 Stream Header", [
                ("ID1", "uint8", 0, 1, null),
                ("ID2", "uint8", 1, 1, null),
                ("Version", "uint8", 2, 1, null),
                ("BlockSize", "uint8", 3, 1, null),
            ], false),
            ("BZip2 Block Header", [
                ("BlockMagic1", "uint8", 4, 1, null),
                ("BlockMagic2", "uint8", 5, 1, null),
                ("BlockMagic3", "uint8", 6, 1, null),
                ("BlockMagic4", "uint8", 7, 1, null),
                ("BlockMagic5", "uint8", 8, 1, null),
                ("BlockMagic6", "uint8", 9, 1, null),
                ("CRC", "uint32", 10, 4, null),
            ], false),
        ]);

    private static FormatRule TtfRule() => CreateRule("TTF", "TrueType/OpenType 字体文件结构",
        [([0x00, 0x01, 0x00, 0x00, 0x00], 0, 12)],
        [
            ("Offset Table", [
                ("sfVersion", "uint32", 0, 4, null),
                ("numTables", "uint16", 4, 2, null),
                ("searchRange", "uint16", 6, 2, null),
                ("entrySelector", "uint16", 8, 2, null),
                ("rangeShift", "uint16", 10, 2, null),
            ], false),
        ]);

    private static FormatRule OtfRule() => CreateRule("OTF", "OpenType 字体文件结构",
        [([0x4F, 0x54, 0x54, 0x4F], 0, 12)],
        [
            ("Offset Table", [
                ("sfVersion", "uint32", 0, 4, null),
                ("numTables", "uint16", 4, 2, null),
                ("searchRange", "uint16", 6, 2, null),
                ("entrySelector", "uint16", 8, 2, null),
                ("rangeShift", "uint16", 10, 2, null),
            ], false),
        ]);

    private static FormatRule Id3v2Rule() => CreateRule("MP3-ID3", "MP3 ID3v2 标签结构 (后续帧位置动态)",
        [([0x49, 0x44, 0x33], 0, 10)],
        [
            ("ID3v2 Header", [
                ("Identifier", "ascii", 0, 3, null),
                ("VersionMajor", "uint8", 3, 1, null),
                ("VersionMinor", "uint8", 4, 1, null),
                ("Flags", "uint8", 5, 1, null),
                ("Size", "bytes", 6, 4, null),
            ], false),
        ]);

    private static FormatRule TiffRule() => CreateRule("TIFF-LE", "TIFF 图片文件结构 (小端)",
        [([0x49, 0x49, 0x2A, 0x00], 0, 8)],
        [
            ("TIFF Header", [
                ("ByteOrder", "ascii", 0, 2, null),
                ("Magic", "uint16", 2, 2, null),
                ("IFD0Offset", "uint32", 4, 4, null),
            ], false),
        ]);

    private static FormatRule TiffBeRule() => CreateRule("TIFF-BE", "TIFF 图片文件结构 (大端)",
        [([0x4D, 0x4D, 0x00, 0x2A], 0, 8)],
        [
            ("TIFF Header", [
                ("ByteOrder", "ascii", 0, 2, null),
                ("Magic", "uint16", 2, 2, null),
                ("IFD0Offset", "uint32", 4, 4, null),
            ], false),
        ]);

    private static FormatRule IcoRule()
    {
        var rule = CreateRule("ICO", "ICO 图标文件结构",
            [([0x00, 0x00, 0x01, 0x00], 0, 6)],
            [
                ("ICONDIR", [
                    ("Reserved", "uint16", 0, 2, null),
                    ("Type", "uint16", 2, 2, null),
                    ("Count", "uint16", 4, 2, null),
                ], false),
                ("ICONDIRENTRY(1)", [
                    ("Width", "uint8", 6, 1, null),
                    ("Height", "uint8", 7, 1, null),
                    ("ColorCount", "uint8", 8, 1, null),
                    ("Reserved", "uint8", 9, 1, null),
                    ("Planes", "uint16", 10, 2, null),
                    ("BitCount", "uint16", 12, 2, null),
                    ("BytesInRes", "uint32", 14, 4, null),
                    ("ImageOffset", "uint32", 18, 4, null),
                ], false),
            ]);
        // Repeating: 每个 ICONDIRENTRY 16 字节，数量由 Count 字段决定
        rule.Structures[1].Repeating = true;
        rule.Structures[1].StepSize = 16;
        rule.Structures[1].CountField = "Count";
        rule.Structures[1].BaseRepeatOffset = 6;
        return rule;
    }

    private static FormatRule MinidumpRule() => CreateRule("Minidump", "Windows Minidump 调试转储文件结构",
        [([0x4D, 0x44, 0x4D, 0x50], 0, 32)],
        [
            ("Minidump Header", [
                ("Signature", "ascii", 0, 4, null),
                ("Version", "uint32", 4, 4, null),
                ("NumberOfStreams", "uint32", 8, 4, null),
                ("StreamDirectoryRva", "uint32", 12, 4, null),
                ("CheckSum", "uint32", 16, 4, null),
                ("TimeDateStamp", "uint32", 20, 4, null),
                ("Flags", "uint64", 24, 8, null),
            ], false),
        ]);

    private static FormatRule SqliteRule() => CreateRule("SQLite", "SQLite 数据库文件结构",
        [([0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00], 0, 100)],
        [
            ("SQLite Database Header", [
                ("Magic", "ascii", 0, 16, null),
                ("PageSize", "uint16", 16, 2, null),
                ("WriteVer", "uint8", 18, 1, null),
                ("ReadVer", "uint8", 19, 1, null),
                ("ReservedSpace", "uint8", 20, 1, null),
                ("MaxPayloadFrac", "uint8", 21, 1, null),
                ("MinPayloadFrac", "uint8", 22, 1, null),
                ("LeafPayloadFrac", "uint8", 23, 1, null),
                ("FileChangeCounter", "uint32", 24, 4, null),
                ("DbSizeInPages", "uint32", 28, 4, null),
                ("FirstFreelistPage", "uint32", 32, 4, null),
                ("TotalFreelistPages", "uint32", 36, 4, null),
                ("SchemaCookie", "uint32", 40, 4, null),
                ("SchemaFormat", "uint32", 44, 4, null),
                ("DefaultCacheSize", "uint32", 48, 4, null),
                ("LargestRootPage", "uint32", 52, 4, null),
                ("TextEncoding", "uint32", 56, 4, null),
                ("UserVersion", "uint32", 60, 4, null),
                ("IncrVacuumMode", "uint32", 64, 4, null),
                ("AppID", "uint32", 68, 4, null),
                ("VersionValidFor", "uint32", 92, 4, null),
                ("SqliteVersion", "uint32", 96, 4, null),
            ], false),
        ]);

    private static FormatRule TsRule() => CreateRule("MPEG-TS", "MPEG-TS 传输流文件结构",
        [([0x47, 0x40, 0x00, 0x10], 0, 188), ([0x47, 0x00, 0x00, 0x00], 0, 188)],
        [
            ("TS Packet Header", [
                ("SyncByte", "uint8", 0, 1, null),
                ("PID_High", "uint16", 1, 2, null),
                ("Flags_CC", "uint8", 3, 1, null),
            ], false),
        ]);

    private static FormatRule IsoRule() => CreateRule("ISO", "ISO 9660 光盘映像文件结构",
        [([0x43, 0x44, 0x30, 0x30, 0x31], 0x8001, 8831)],
        [
            ("Primary Volume Descriptor", [
                ("TypeCode", "uint8", 0x8000, 1, null),
                ("Identifier", "ascii", 0x8001, 5, null),
                ("Version", "uint8", 0x8006, 1, null),
                ("SystemIdentifier", "ascii", 0x8008, 32, null),
                ("VolumeIdentifier", "ascii", 0x8028, 32, null),
                ("VolumeSpaceSize", "uint32", 0x8050, 8, null),
                ("LogicalBlockSize", "uint32", 0x8080, 4, null),
                ("RootDirRecord", "bytes", 0x809C, 34, null),
                ("VolumeSetIdentifier", "ascii", 0x80BE, 128, null),
                ("PublisherIdentifier", "ascii", 0x813E, 128, null),
                ("DataPreparerIdentifier", "ascii", 0x81BE, 128, null),
                ("ApplicationIdentifier", "ascii", 0x823E, 128, null),
                ("CreateDateTime", "ascii", 0x832D, 17, null),
                ("ModifyDateTime", "ascii", 0x833E, 17, null),
            ], false),
        ]);

    private static FormatRule RtfRule() => CreateRule("RTF", "RTF 富文本格式文件结构",
        [([0x7B, 0x5C, 0x72, 0x74, 0x66], 0, 14)],
        [
            ("RTF Header", [
                ("OpenBrace", "ascii", 0, 1, null),
                ("BackslashRtf", "ascii", 1, 4, null),
                ("Encoding", "ascii", 5, 2, null),
                ("Ansicpg", "ascii", 7, 7, null),
            ], false),
        ]);

    private static FormatRule WoffRule() => CreateRule("WOFF", "WOFF 网页字体结构",
        [([0x77, 0x4F, 0x46, 0x46], 0, 20)],
        [
            ("WOFF Header", [
                ("Signature", "ascii", 0, 4, null),
                ("Flavor", "uint32", 4, 4, null),
                ("Length", "uint32", 8, 4, null),
                ("NumTables", "uint16", 12, 2, null),
                ("Reserved", "uint16", 14, 2, null),
                ("TotalSfntSize", "uint32", 16, 4, null),
            ], false),
        ]);

    private static FormatRule Woff2Rule() => CreateRule("WOFF2", "WOFF2 网页字体结构",
        [([0x77, 0x4F, 0x46, 0x32], 0, 20)],
        [
            ("WOFF2 Header", [
                ("Signature", "ascii", 0, 4, null),
                ("Flavor", "uint32", 4, 4, null),
                ("Length", "uint32", 8, 4, null),
                ("NumTables", "uint16", 12, 2, null),
                ("Reserved", "uint16", 14, 2, null),
                ("TotalSfntSize", "uint32", 16, 4, null),
            ], false),
        ]);

    private static FormatRule Macho32Rule() => CreateRule("Mach-O-32", "Mach-O 可执行文件 (32位)",
        [([0xFE, 0xED, 0xFA, 0xCE], 0, 28), ([0xCE, 0xFA, 0xED, 0xFE], 0, 28)],
        [
            ("Mach-O Header (32-bit)", [
                ("Magic", "uint32", 0, 4, null),
                ("CPUType", "int32", 4, 4, null),
                ("CPUSubType", "int32", 8, 4, null),
                ("FileType", "uint32", 12, 4, null),
                ("NumLoadCommands", "uint32", 16, 4, null),
                ("SizeOfLoadCommands", "uint32", 20, 4, null),
                ("Flags", "uint32", 24, 4, null),
            ], false),
        ]);

    private static FormatRule Macho64Rule() => CreateRule("Mach-O-64", "Mach-O 可执行文件 (64位)",
        [([0xFE, 0xED, 0xFA, 0xCF], 0, 32), ([0xCF, 0xFA, 0xED, 0xFE], 0, 32)],
        [
            ("Mach-O Header (64-bit)", [
                ("Magic", "uint32", 0, 4, null),
                ("CPUType", "int32", 4, 4, null),
                ("CPUSubType", "int32", 8, 4, null),
                ("FileType", "uint32", 12, 4, null),
                ("NumLoadCommands", "uint32", 16, 4, null),
                ("SizeOfLoadCommands", "uint32", 20, 4, null),
                ("Flags", "uint32", 24, 4, null),
                ("Reserved", "uint32", 28, 4, null),
            ], false),
        ]);

    private static FormatRule AiffRule() => CreateRule("AIFF", "AIFF 音频文件结构",
        [([0x46, 0x4F, 0x52, 0x4D], 0, 12)],
        [
            ("FORM Header", [
                ("ChunkID", "ascii", 0, 4, null),
                ("ChunkSize", "uint32", 4, 4, null),
                ("Format", "ascii", 8, 4, null),
            ], false),
            ("Common Chunk", [
                ("ChunkID", "ascii", 12, 4, null),
                ("ChunkSize", "uint32", 16, 4, null),
                ("NumChannels", "uint16", 20, 2, null),
                ("NumSampleFrames", "uint32", 22, 4, null),
                ("SampleSize", "uint16", 26, 2, null),
                ("SampleRate", "uint80", 28, 10, null),
            ], false),
        ]);

    private static FormatRule DebRule() => CreateRule("DEB", "Debian 软件包结构",
        [([0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E], 0, 68)],
        [
            ("ar Archive Header", [
                ("Magic", "ascii", 0, 8, null),
            ], false),
            ("debian-binary Entry", [
                ("FileName", "ascii", 8, 16, null),
                ("FileModTime", "ascii", 24, 12, null),
                ("OwnerID", "ascii", 36, 6, null),
                ("GroupID", "ascii", 42, 6, null),
                ("FileMode", "ascii", 48, 8, null),
                ("FileSize", "ascii", 56, 10, null),
                ("TrailingMagic", "ascii", 66, 2, null),
            ], false),
            ("control.tar.gz Entry", [
                ("FileName", "ascii", 68, 16, null),
                ("FileModTime", "ascii", 84, 12, null),
                ("OwnerID", "ascii", 96, 6, null),
                ("GroupID", "ascii", 102, 6, null),
                ("FileMode", "ascii", 108, 8, null),
                ("FileSize", "ascii", 116, 10, null),
                ("TrailingMagic", "ascii", 126, 2, null),
            ], false),
        ]);

    private static FormatRule Ole2Rule() => CreateRule("OLE2", "OLE2 复合文档 (DOC/XLS/PPT/MSI)",
        [([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], 0, 24)],
        [
            ("OLE2 Header", [
                ("Magic", "bytes", 0, 8, null),
                ("CLSID", "bytes", -1, 16, null),
                ("MinorVersion", "uint16", -1, 2, null),
                ("MajorVersion", "uint16", -1, 2, null),
                ("ByteOrder", "uint16", -1, 2, null),
                ("SectorShift", "uint16", -1, 2, null),
                ("MiniSectorShift", "uint16", -1, 2, null),
                ("Reserved", "bytes", -1, 6, null),
                ("NumDirectorySectors", "uint32", -1, 4, null),
                ("NumFATs", "uint32", -1, 4, null),
                ("FirstDirectorySector", "uint32", -1, 4, null),
                ("TransactionSig", "uint32", -1, 4, null),
                ("MiniStreamCutoff", "uint32", -1, 4, null),
                ("FirstMiniFATSector", "uint32", -1, 4, null),
                ("NumMiniFATs", "uint32", -1, 4, null),
            ], true),
        ]);

    private static FormatRule RarRule() => CreateRule("RAR", "RAR 压缩包文件结构",
        [([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07], 0, 13)],
        [
            ("RAR Header", [
                ("Magic", "bytes", 0, 7, null),
                ("HeaderCRC", "uint16", 7, 2, null),
                ("HeaderType", "uint8", 9, 1, null),
                ("HeaderFlags", "uint16", 10, 2, null),
                ("ExtraSize", "uint16", 12, 2, null),
            ], false),
        ]);

    private static FormatRule PsdRule() => CreateRule("PSD", "PSD 图片文件结构",
        [([0x38, 0x42, 0x50, 0x53], 0, 26)],
        [
            ("PSD Header", [
                ("Signature", "ascii", 0, 4, null),
                ("Version", "uint16", 4, 2, null),
                ("Reserved", "bytes", 6, 6, null),
                ("Channels", "uint16", 12, 2, null),
                ("Rows", "uint32", 14, 4, null),
                ("Columns", "uint32", 18, 4, null),
                ("BitDepth", "uint16", 22, 2, null),
                ("Mode", "uint16", 24, 2, null),
            ], false),
        ]);

    private static FormatRule PdfRule() => CreateRule("PDF", "PDF 文档文件结构",
        [([0x25, 0x50, 0x44, 0x46], 0, 8)],
        [
            ("PDF Header", [
                ("Magic", "ascii", 0, 5, null),
                ("MajorVersion", "uint8", 5, 1, null),
                ("DotSeparator", "uint8", 6, 1, null),
                ("MinorVersion", "uint8", 7, 1, null),
            ], false),
        ]);

    private static FormatRule OggRule() => CreateRule("OGG", "OGG 音频/视频容器格式",
        [([0x4F, 0x67, 0x67, 0x53], 0, 28)],
        [
            ("OGG Page Header", [
                ("CapturePattern", "ascii", 0, 4, null),
                ("Version", "uint8", 4, 1, null),
                ("HeaderType", "uint8", 5, 1, null),
                ("GranulePosition", "uint64", 6, 8, null),
                ("BitstreamSerial", "uint32", 14, 4, null),
                ("PageSeqNo", "uint32", 18, 4, null),
                ("PageChecksum", "uint32", 22, 4, null),
                ("PageSegments", "uint8", 26, 1, null),
            ], false),
        ]);

    private static FormatRule Mp4Rule() => CreateRule("MP4", "MP4 视频文件结构",
        [([0x66, 0x74, 0x79, 0x70], 4, 8)],
        [
            ("ftyp Box", [
                ("BoxSize", "uint32", 0, 4, "BigEndian"),
                ("BoxType", "ascii", 4, 4, null),
                ("MajorBrand", "ascii", 8, 4, null),
                ("MinorVersion", "uint32", 12, 4, "BigEndian"),
                ("CompatibleBrands", "bytes", 16, 16, null),
            ], false),
            ("FileBody", [
                ("Content", "bytes", 32, 0x100000, null),
            ], false),
        ]);
}
