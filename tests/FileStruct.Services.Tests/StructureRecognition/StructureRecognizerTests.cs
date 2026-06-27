using System.Text;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using FileStruct.Infrastructure.Configuration;
using FileStruct.Services.StructureRecognition;
using RuleEngineSvc = FileStruct.Services.RuleEngine.RuleEngine;
using Moq;

namespace FileStruct.Services.Tests.StructureRecognition;

public class StructureRecognizerTests : IDisposable
{
    private readonly string _testDir;
    private readonly StructureRecognizer _recognizer;
    private readonly Mock<ILogService> _loggerMock;

    public StructureRecognizerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructPackerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        _loggerMock = new Mock<ILogService>();
        _loggerMock
            .Setup(l => l.BeginOperation(It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());

        // 使用真实服务组建识别管道
        var signatureMatcher = new SignatureMatcher();
        var confidenceScorer = new ConfidenceScorer();
        var ruleEngine = new RuleEngineSvc(_loggerMock.Object);
        foreach (var rule in BuiltinRuleProvider.GetAll())
            ruleEngine.AddBuiltinRule(rule);

        _recognizer = new StructureRecognizer(
            signatureMatcher, confidenceScorer, ruleEngine, _loggerMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    // ============================================================
    //  节区表解析测试
    // ============================================================

    [Fact]
    public void Recognize_PE_WithNormalSections_ParsesSectionTable()
    {
        var filePath = CreateMinimalPe([".text", ".data", ".rsrc"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);

        var sectionTable = peNode.Children.FirstOrDefault(c => c.Name != null && c.Name.Contains("Section Table"));
        Assert.NotNull(sectionTable);
        Assert.Equal(3, sectionTable.Children.Count);

        // 节区名应从 ASCII 正确读取
        Assert.Contains(sectionTable.Children, c => c.Name == ".text");
        Assert.Contains(sectionTable.Children, c => c.Name == ".data");
        Assert.Contains(sectionTable.Children, c => c.Name == ".rsrc");
    }

    [Fact]
    public void Recognize_PE_WithNormalSections_DoesNotTriggerPackerDetection()
    {
        var filePath = CreateMinimalPe([".text", ".data", ".rsrc"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);

        // 标准节区名不应触发加壳检测
        Assert.False(peNode.Children.Any(c => c.Name is { } n && n.Contains("🔒")),
            "标准 PE 节区不应触发加壳检测");
    }

    // ============================================================
    //  常见加壳检测
    // ============================================================

    [Fact]
    public void Recognize_PE_WithUPXSections_DetectsUPX()
    {
        var filePath = CreateMinimalPe([".upx0", ".upx1"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("UPX"));
    }

    [Fact]
    public void Recognize_PE_WithASPackSections_DetectsASPack()
    {
        var filePath = CreateMinimalPe([".aspack", ".adata"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("ASPack"));
    }

    [Fact]
    public void Recognize_PE_WithPetiteSection_DetectsPetite()
    {
        var filePath = CreateMinimalPe([".petite", ".text"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("Petite"));
    }

    [Fact]
    public void Recognize_PE_WithMPRESSSections_DetectsMPRESS()
    {
        var filePath = CreateMinimalPe([".mpress1", ".mpress2"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("MPRESS"));
    }

    [Fact]
    public void Recognize_PE_WithVMProtectSections_DetectsVMProtect()
    {
        var filePath = CreateMinimalPe([".vmp0", ".vmp1"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("VMProtect"));
    }

    [Fact]
    public void Recognize_PE_WithThemidaSections_DetectsThemida()
    {
        var filePath = CreateMinimalPe([".themida", ".safeweb"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("Themida"));
    }

    [Fact]
    public void Recognize_PE_WithEnigmaSections_DetectsEnigma()
    {
        var filePath = CreateMinimalPe([".enigma", ".enigma1"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("Enigma"));
    }

    [Fact]
    public void Recognize_PE_WithArmadilloSections_DetectsArmadillo()
    {
        var filePath = CreateMinimalPe([".adata", ".cdata"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("Armadillo"));
    }

    [Fact]
    public void Recognize_PE_WithRLPackSection_DetectsRLPack()
    {
        var filePath = CreateMinimalPe([".rlpack", ".text"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("RLPack"));
    }

    [Fact]
    public void Recognize_PE_WithEXECryptorSections_DetectsEXECryptor()
    {
        var filePath = CreateMinimalPe([".ecrypt", ".text"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("EXECryptor"));
    }

    [Fact]
    public void Recognize_PE_WithStarForceSections_DetectsStarForce()
    {
        var filePath = CreateMinimalPe([".!sf", ".sf_"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);
        Assert.Contains(peNode.Children, c => c.Name is { } n && n.Contains("StarForce"));
    }

    // ============================================================
    //  边界情况
    // ============================================================

    [Fact]
    public void Recognize_PE_SingleSection_StillParsesCorrectly()
    {
        var filePath = CreateMinimalPe(["UPX0"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);

        var sectionTable = peNode.Children.FirstOrDefault(c => c.Name != null && c.Name.Contains("Section Table"));
        Assert.NotNull(sectionTable);
        Assert.Single(sectionTable.Children);
    }

    [Fact]
    public void Recognize_PE_EmptySectionName_UsesFallbackName()
    {
        var filePath = CreateMinimalPe([""]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var peNode = result.Children.FirstOrDefault(c => c.Name == "PE");
        Assert.NotNull(peNode);

        var sectionTable = peNode.Children.FirstOrDefault(c => c.Name != null && c.Name.Contains("Section Table"));
        Assert.NotNull(sectionTable);
        // 空节区名应使用 Section[0] 作为回退名
        var section = sectionTable.Children.FirstOrDefault();
        Assert.NotNull(section);
        Assert.Equal("Section[0]", section.Name);
    }

    // ============================================================
    //  辅助：创建最小化的 PE 测试文件
    // ============================================================

    /// <summary>
    /// 创建包含给定节区名的 PE 文件。节名是加壳检测的关键特征。
    /// 使用 <![CDATA[e_lfanew = 0x80]]>、标准 PE32 Optional Header (0xE0 字节)。
    /// </summary>
    private string CreateMinimalPe(string[] sectionNames)
    {
        const int eLfanew = 0x80;
        const ushort sizeOptHeader = 0xE0; // PE32 标准 Optional Header 大小

        var data = new byte[4096];
        using var ms = new MemoryStream(data);
        using var writer = new BinaryWriter(ms);

        // ── DOS Header (0–63) ──
        writer.Write((ushort)0x5A4D); // e_magic: MZ
        writer.BaseStream.Position = 60;
        writer.Write((uint)eLfanew); // e_lfanew

        // ── PE Signature (0x80–0x83) ──
        writer.BaseStream.Position = eLfanew;
        writer.Write(0x00004550u); // "PE\0\0"

        // ── COFF File Header (0x84–0x97) ──
        writer.Write((ushort)0x014C);     // Machine: i386
        writer.Write((ushort)sectionNames.Length); // NumberOfSections
        writer.Write(0u);                 // TimeDateStamp
        writer.Write(0u);                 // PointerToSymbolTable
        writer.Write(0u);                 // NumberOfSymbols
        writer.Write(sizeOptHeader);      // SizeOfOptionalHeader
        writer.Write((ushort)0x0102);     // Characteristics: EXE | 32BIT

        // ── Optional Header (0x98–0x177 = 224 bytes) ──
        var optHeaderStart = eLfanew + 24; // = 0x98
        writer.BaseStream.Position = optHeaderStart;

        writer.Write((ushort)0x010B); // Magic: PE32
        writer.Write((byte)10);       // MajorLinkerVersion
        writer.Write((byte)11);       // MinorLinkerVersion
        writer.Write(0u);             // SizeOfCode
        writer.Write(0u);             // SizeOfInitializedData
        writer.Write(0u);             // SizeOfUninitializedData
        writer.Write(0x1000u);        // AddressOfEntryPoint (e_lfanew + 40 = 0xA8)
        writer.Write(0x1000u);        // BaseOfCode

        // 填充到 SizeOfOptionalHeader 字节
        writer.BaseStream.Position = optHeaderStart + sizeOptHeader;

        // ── Section Table (0x178 起) ──
        var sectionTableOffset = eLfanew + 24 + sizeOptHeader; // = 0x178
        writer.BaseStream.Position = sectionTableOffset;

        for (int i = 0; i < sectionNames.Length; i++)
        {
            var raw = Encoding.ASCII.GetBytes(sectionNames[i]);
            writer.Write(raw);
            // 补零至 8 字节
            for (int p = raw.Length; p < 8; p++)
                writer.Write((byte)0);

            writer.Write(0x1000u);                        // VirtualSize
            writer.Write((uint)(0x1000 + i * 0x1000));    // VirtualAddress
            writer.Write(0x1000u);                        // SizeOfRawData (与 VirtualSize 相等，避免启发式误报)
            writer.Write(0x200u);                         // PointerToRawData
            writer.Write(0u);                             // PointerToRelocations
            writer.Write(0u);                             // PointerToLinenumbers
            writer.Write((ushort)0);                      // NumberOfRelocations
            writer.Write((ushort)0);                      // NumberOfLinenumbers
            writer.Write(0x60000020u);                    // Characteristics: CODE | EXECUTE | READ
        }

        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(path, data);
        return path;
    }

    // ============================================================
    //  APK 识别测试
    // ============================================================

    [Fact]
    public void Recognize_ApkFile_DetectsKeyComponents()
    {
        var filePath = CreateMinimalApk(["AndroidManifest.xml", "classes.dex", "resources.arsc"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var apkNode = result.Children.FirstOrDefault(c => c.Name == "APK");
        Assert.NotNull(apkNode);

        var components = apkNode.Children
            .FirstOrDefault(c => c.Name != null && c.Name.Contains("APK Components"));
        Assert.NotNull(components);
        Assert.Equal(3, components.Children.Count);
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("AndroidManifest.xml"));
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("classes.dex"));
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("resources.arsc"));
    }

    [Fact]
    public void Recognize_ZipFile_DoesNotTriggerApkProcessing()
    {
        var filePath = CreateMinimalApk(["file.txt"]);
        var zipPath = Path.ChangeExtension(filePath, ".zip");
        File.Move(filePath, zipPath);
        using var buffer = BinaryBuffer.LoadFromFile(zipPath);

        var result = _recognizer.Recognize(buffer);

        Assert.DoesNotContain(result.Children, c => c.Name == "APK");
        Assert.Contains(result.Children, c => c.Name == "ZIP");
    }

    [Fact]
    public void Recognize_ApkFile_DetectsNativeLibs()
    {
        var filePath = CreateMinimalApk(["AndroidManifest.xml", "lib/armeabi-v7a/libnative.so"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var apkNode = result.Children.FirstOrDefault(c => c.Name == "APK");
        Assert.NotNull(apkNode);

        var components = apkNode.Children
            .FirstOrDefault(c => c.Name != null && c.Name.Contains("APK Components"));
        Assert.NotNull(components);
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("lib/armeabi-v7a/libnative.so"));
    }

    /// <summary>创建最小 APK 文件（实质为 ZIP + .apk 扩展名），包含指定条目</summary>
    private string CreateMinimalApk(string[] entryNames)
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.apk");
        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, false);

        foreach (var name in entryNames)
        {
            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"content_{name}");
        }

        return path;
    }

    // ============================================================
    //  EPUB/MOBI 识别测试
    // ============================================================

    [Fact]
    public void Recognize_EpubFile_DetectsKeyComponents()
    {
        var filePath = CreateMinimalEpub(["mimetype", "META-INF/container.xml", "OEBPS/content.opf"]);
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var epubNode = result.Children.FirstOrDefault(c => c.Name == "EPUB");
        Assert.NotNull(epubNode);

        var components = epubNode.Children
            .FirstOrDefault(c => c.Name != null && c.Name.Contains("EPUB Components"));
        Assert.NotNull(components);
        Assert.Equal(3, components.Children.Count);
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("mimetype"));
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("container.xml"));
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("content.opf"));
    }

    [Fact]
    public void Recognize_ZipFile_DoesNotTriggerEpubProcessing()
    {
        var filePath = CreateMinimalEpub(["file.txt"]);
        var zipPath = Path.ChangeExtension(filePath, ".zip");
        File.Move(filePath, zipPath);
        using var buffer = BinaryBuffer.LoadFromFile(zipPath);

        var result = _recognizer.Recognize(buffer);

        Assert.DoesNotContain(result.Children, c => c.Name == "EPUB");
        Assert.Contains(result.Children, c => c.Name == "ZIP");
    }

    [Fact]
    public void Recognize_MobiFile_DetectsPalmDbHeader()
    {
        var filePath = CreateMinimalMobi();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var mobiNode = result.Children.FirstOrDefault(c => c.Name == "MOBI");
        Assert.NotNull(mobiNode);
        Assert.Contains(mobiNode.Children, c => c.Name != null && c.Name.Contains("PalmDB Header"));
    }

    [Fact]
    public void Recognize_CrxV2File_DetectsKeyComponents()
    {
        // 创建 CRX v2: 16 字节头 + ZIP 数据
        var zipPath = CreateMinimalZip(["manifest.json", "icon.png"]);
        var zipBytes = File.ReadAllBytes(zipPath);
        var crxPath = Path.Combine(_testDir, $"{Guid.NewGuid():N}.crx");
        using (var fs = new FileStream(crxPath, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            // CRX v2 头: Cr24 + version=2 + pubKeyLen=0 + sigLen=0
            writer.Write((uint)0x34327243); // "Cr24" (LE)
            writer.Write(2u);               // version = 2
            writer.Write(0u);               // pubKeyLen = 0
            writer.Write(0u);               // sigLen = 0
            writer.Write(zipBytes);         // ZIP 数据紧随其后
        }
        using var buffer = BinaryBuffer.LoadFromFile(crxPath);
        var result = _recognizer.Recognize(buffer);

        var crxNode = result.Children.FirstOrDefault(c => c.Name == "CRX");
        Assert.NotNull(crxNode);
        Assert.Contains(crxNode.Children, c => c.Name != null && c.Name.Contains("ZIP Data"));

        var components = crxNode.Children
            .FirstOrDefault(c => c.Name != null && c.Name.Contains("CRX Components"));
        Assert.NotNull(components);
        Assert.Contains(components.Children, c => c.Name != null && c.Name.Contains("manifest.json"));
    }

    /// <summary>
    /// 创建含指定条目的最小 ZIP 文件，返回文件路径。
    /// 用于构造 CRX 测试文件的 ZIP 数据部分。
    /// </summary>
    private string CreateMinimalZip(string[] entryNames)
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.zip");
        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, false);
        foreach (var name in entryNames)
        {
            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"content_{name}");
        }
        return path;
    }

    /// <summary>创建最小 EPUB 文件（ZIP + .epub 扩展名）</summary>
    private string CreateMinimalEpub(string[] entryNames)
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.epub");
        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, false);

        foreach (var name in entryNames)
        {
            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"content_{name}");
        }

        return path;
    }

    /// <summary>创建最小 MOBI 文件（PalmDB 头 + BOOKMOBI 魔数）</summary>
    private string CreateMinimalMobi()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.mobi");
        var data = new byte[256];

        // PalmDB 头 78 字节 + Record Info 8 字节 + MOBI 头起始
        // BOOKMOBI 在偏移 0x3C (60)
        data[0x3C] = (byte)'B'; data[0x3D] = (byte)'O';
        data[0x3E] = (byte)'O'; data[0x3F] = (byte)'K';
        data[0x40] = (byte)'M'; data[0x41] = (byte)'O';
        data[0x42] = (byte)'B'; data[0x43] = (byte)'I';

        // NumRecords = 1 在偏移 76
        data[76] = 1; data[77] = 0;

        // Record[0] 描述符在偏移 78: dataOff=0x80, attr=0, uniqueID=0
        data[78] = 0x80; data[79] = 0x00; data[80] = 0x00; data[81] = 0x00;
        data[82] = 0; data[83] = 0; data[84] = 0; data[85] = 0;

        // MOBI Header 在偏移 0x80
        data[0x80] = (byte)'M'; data[0x81] = (byte)'O';
        data[0x82] = (byte)'B'; data[0x83] = (byte)'I';
        // HeaderLength = 232 (BigEndian) 在 0x84
        data[0x84] = 0; data[0x85] = 0; data[0x86] = 0; data[0x87] = 232;
        // MobiType = 2 (Book, BigEndian) 在 0x88
        data[0x88] = 0; data[0x89] = 0; data[0x8A] = 0; data[0x8B] = 2;

        File.WriteAllBytes(path, data);
        return path;
    }

    // ============================================================
    //  LNK 测试
    // ============================================================

    [Fact]
    public void Recognize_LnkWithDataStrings_ParsesTargetPath()
    {
        // 创建一个最小 LNK: Shell Link Header(76) + RelativePath 数据字符串(Unicode)
        var filePath = CreateMinimalLnk();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var lnkNode = result.Children.FirstOrDefault(c => c.Name == "LNK");
        Assert.NotNull(lnkNode);

        // 验证 LinkFlags 被解码
        Assert.Contains(lnkNode.Children, c => c.Name != null && c.Name.Contains("LinkFlags"));

        // 验证 RelativePath 被提取（linkFlags 中 HasRelativePath 置位）
        Assert.Contains(lnkNode.Children, c => c.Name == "RelativePath");
    }

    /// <summary>创建最小 LNK 文件（完整 Shell Link Header + RelativePath）</summary>
    private string CreateMinimalLnk()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.lnk");
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Shell Link Header (76 bytes) — 规范要求 HeaderSize = 0x4C
        writer.Write(0x0000004Cu);  // HeaderSize
        writer.Write(new byte[] { 0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
                                  0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 }); // CLSID

        // LinkFlags: HasRelativePath(bit3) + IsUnicode(bit7) = 0x88
        writer.Write(0x88u);
        writer.Write(0u); // FileAttributes
        writer.Write(0L); // CreationTime
        writer.Write(0L); // AccessTime
        writer.Write(0L); // WriteTime
        writer.Write(0u); // FileSize
        writer.Write(0);  // IconIndex
        writer.Write(0u); // ShowCommand
        writer.Write((ushort)0); // Hotkey
        writer.Write((ushort)0); // Reserved1
        writer.Write(0u);        // Reserved2
        writer.Write(0u);        // Reserved3
        // Header done at offset 76

        // RelativePath (Unicode string): charCount prefix + UTF-16LE data
        var relPath = @"target\file.txt";
        writer.Write((ushort)relPath.Length); // charCount
        writer.Write(Encoding.Unicode.GetBytes(relPath));

        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }

    // ============================================================
    //  DMG 测试
    // ============================================================

    [Fact]
    public void Recognize_DmgFile_DetectsKolyFooter()
    {
        var filePath = CreateMinimalDmg();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var dmgNode = result.Children.FirstOrDefault(c => c.Name == "DMG");
        Assert.NotNull(dmgNode);

        Assert.Contains(dmgNode.Children, c => c.Name != null && c.Name.Contains("Koly Footer"));
    }

    /// <summary>创建最小 DMG 文件（zlib 魔数 + Koly 尾部块）</summary>
    private string CreateMinimalDmg()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.dmg");
        var fileSize = 1024;

        var data = new byte[fileSize];

        // zlib 头 (7 字节) — 匹配 BuiltinSignatures 的 DMG 签名
        data[0] = 0x78; data[1] = 0x01; data[2] = 0x73;
        data[3] = 0x0D; data[4] = 0x62; data[5] = 0x62; data[6] = 0x60;

        // Koly Footer 从偏移 512 开始
        int kolyOff = fileSize - 512; // = 512
        WriteBytes(data, kolyOff, [0x6B, 0x6F, 0x6C, 0x79]); // "koly"
        WriteU32LE(data, kolyOff + 4, 4u);    // Version
        WriteU32LE(data, kolyOff + 8, 1u);    // HeaderSize
        WriteU32LE(data, kolyOff + 12, 0u);   // Flags
        WriteU64LE(data, kolyOff + 28, 0uL);  // DataForkOffset
        WriteU64LE(data, kolyOff + 36, 512uL);// DataForkLength
        WriteU64LE(data, kolyOff + 44, 0uL);  // RsrcForkOffset
        WriteU64LE(data, kolyOff + 52, 0uL);  // RsrcForkLength
        WriteU32LE(data, kolyOff + 60, 0u);   // SegmentNumber
        WriteU32LE(data, kolyOff + 64, 1u);   // SegmentCount
        WriteU64LE(data, kolyOff + 72, 0uL);  // BlkxOffset
        WriteU64LE(data, kolyOff + 80, 0uL);  // BlkxCount

        // 尾部 "koly" 签名（最后 4 字节）
        WriteBytes(data, fileSize - 4, [0x6B, 0x6F, 0x6C, 0x79]);

        File.WriteAllBytes(path, data);
        return path;
    }

    private static void WriteBytes(byte[] data, int offset, byte[] bytes)
    {
        Array.Copy(bytes, 0, data, offset, bytes.Length);
    }

    private static void WriteU32LE(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteU64LE(byte[] data, int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            data[offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    // ============================================================
    //  PYC 测试
    // ============================================================

    [Fact]
    public void Recognize_PycFile_DetectsVersionAndHeader()
    {
        var filePath = CreateMinimalPyc();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var pycNode = result.Children.FirstOrDefault(c => c.Name == "PYC");
        Assert.NotNull(pycNode);

        // 验证版本节点
        Assert.Contains(pycNode.Children, c => c.Name != null && c.Name.Contains("Python Version"));
        // 验证 BitField
        Assert.Contains(pycNode.Children, c => c.Name == "BitField");
        // 验证时间戳
        Assert.Contains(pycNode.Children, c => c.Name == "Timestamp");
        // 验证源代码大小
        Assert.Contains(pycNode.Children, c => c.Name == "SourceSize");
    }

    /// <summary>创建最小 PYC 文件（Python 3.8 格式）</summary>
    private string CreateMinimalPyc()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.pyc");
        var data = new byte[64];

        // Magic: Python 3.8 = 55 0D 0D 0A
        data[0] = 0x55; data[1] = 0x0D; data[2] = 0x0D; data[3] = 0x0A;
        // BitField = 0 (timestamp mode)
        data[4] = 0; data[5] = 0; data[6] = 0; data[7] = 0;
        // Timestamp = 0
        data[8] = 0; data[9] = 0; data[10] = 0; data[11] = 0;
        // SourceSize = 256
        data[12] = 0; data[13] = 1; data[14] = 0; data[15] = 0;
        // Marshalled code object: type='c' (0x63), argcount=0, nlocals=0
        data[16] = 0x63; data[17] = 0; data[18] = 0; data[19] = 0; data[20] = 0;
        data[21] = 0; data[22] = 0; data[23] = 0; data[24] = 0;

        File.WriteAllBytes(path, data);
        return path;
    }

    // ============================================================
    //  PAK 测试
    // ============================================================

    [Fact]
    public void Recognize_PakFile_DetectsVersionAndHeader()
    {
        var filePath = CreateMinimalPak();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var pakNode = result.Children.FirstOrDefault(c => c.Name == "PAK");
        Assert.NotNull(pakNode);

        // PAK Header 结构包含 Version 字段
        var header = pakNode.Children.FirstOrDefault(c => c.Name == "PAK Header");
        Assert.NotNull(header);
        Assert.Contains(header.Children, c => c.Name == "Version");
    }

    /// <summary>创建最小 PAK 文件（"PACK" + UE4 v8 格式）</summary>
    private string CreateMinimalPak()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.pak");
        var data = new byte[256];

        // Header: PACK(4) + Version=8(4) + SubVersion=0(4) + bEncrypted=0(4)
        data[0] = (byte)'P'; data[1] = (byte)'A'; data[2] = (byte)'C'; data[3] = (byte)'K';
        WriteU32LE(data, 4, 8u);  // Version 8
        WriteU32LE(data, 8, 0u);  // SubVersion
        WriteU32LE(data, 12, 0u); // bEncrypted

        // 模拟一个简单的索引格式（UE4 v8）：
        // Int32 mountPointLen=0, Int32 fileCount=0, Int64 indexOffset=块起始
        // 尾部 8 字节: uint64 indexOffset, 4 字节 "PACK"
        var trailerOffset = data.Length - 12;
        WriteU64LE(data, trailerOffset, 0uL);           // indexOffset = 0
        data[trailerOffset + 8] = (byte)'P';           // "PACK"
        data[trailerOffset + 9] = (byte)'A';
        data[trailerOffset + 10] = (byte)'C';
        data[trailerOffset + 11] = (byte)'K';

        File.WriteAllBytes(path, data);
        return path;
    }

    // ============================================================
    //  CAB 测试
    // ============================================================

    [Fact]
    public void Recognize_CabFile_DetectsHeader()
    {
        var filePath = CreateMinimalCab();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var cabNode = result.Children.FirstOrDefault(c => c.Name == "CAB");
        Assert.NotNull(cabNode);

        var header = cabNode.Children.FirstOrDefault(c => c.Name == "CFHEADER");
        Assert.NotNull(header);
        Assert.Contains(header.Children, c => c.Name == "NumFiles");
    }

    /// <summary>创建最小 CAB 文件（MSCF 头 + 空文件列表）</summary>
    private string CreateMinimalCab()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.cab");
        var data = new byte[64];

        // CFHEADER (36 bytes)
        data[0] = (byte)'M'; data[1] = (byte)'S'; data[2] = (byte)'C'; data[3] = (byte)'F';
        WriteU32LE(data, 4, 64u);     // CabinetSize = 64
        WriteU32LE(data, 8, 0u);      // Reserved1
        WriteU32LE(data, 12, 36u);    // FilesOffset = 36 (right after header)
        WriteU32LE(data, 16, 0u);     // Reserved2
        data[20] = 3;                 // MinorVersion
        data[21] = 1;                 // MajorVersion
        WriteU16LE(data, 22, 0);      // NumFolders = 0
        WriteU16LE(data, 24, 0);      // NumFiles = 0
        WriteU16LE(data, 26, 0);      // Flags
        WriteU16LE(data, 28, 0);      // SetID
        WriteU16LE(data, 30, 0);      // CabinetNumber

        File.WriteAllBytes(path, data);
        return path;
    }

    private static void WriteU16LE(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    // ============================================================
    //  7z 测试
    // ============================================================

    [Fact]
    public void Recognize_7zFile_DetectsHeader()
    {
        var filePath = CreateMinimal7z();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var szNode = result.Children.FirstOrDefault(c => c.Name == "7z");
        Assert.NotNull(szNode);

        var header = szNode.Children.FirstOrDefault(c => c.Name == "7z Start Header");
        Assert.NotNull(header);
        Assert.Contains(header.Children, c => c.Name == "Version");
        Assert.Contains(header.Children, c => c.Name == "NextHeaderOffset");
    }

    /// <summary>创建最小 7z 文件头（32 字节）</summary>
    private string CreateMinimal7z()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.7z");
        var data = new byte[32];

        // Signature: 7z\xBC\xAF\x27\x1C
        data[0] = 0x37; data[1] = 0x7A; data[2] = 0xBC; data[3] = 0xAF;
        data[4] = 0x27; data[5] = 0x1C;
        // Version: 0.23
        data[6] = 0; data[7] = 23;
        // StartCRC, NextOffset, NextSize, NextCRC all zero

        File.WriteAllBytes(path, data);
        return path;
    }

    // ============================================================
    //  TAR + GZip 测试
    // ============================================================

    [Fact]
    public void Recognize_TarFile_DetectsHeader()
    {
        var filePath = CreateMinimalTar();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var tarNode = result.Children.FirstOrDefault(c => c.Name == "TAR");
        Assert.NotNull(tarNode);

        var header = tarNode.Children.FirstOrDefault(c => c.Name == "TAR Header");
        Assert.NotNull(header);
        Assert.Contains(header.Children, c => c.Name == "Magic");
        Assert.Contains(header.Children, c => c.Name == "FileName");
    }

    [Fact]
    public void Recognize_GzipFile_DetectsHeader()
    {
        var filePath = CreateMinimalGzip();
        using var buffer = BinaryBuffer.LoadFromFile(filePath);

        var result = _recognizer.Recognize(buffer);

        var gzNode = result.Children.FirstOrDefault(c => c.Name == "GZip");
        Assert.NotNull(gzNode);

        var header = gzNode.Children.FirstOrDefault(c => c.Name == "GZip Header");
        Assert.NotNull(header);
        Assert.Contains(header.Children, c => c.Name == "MTIME");
        Assert.Contains(header.Children, c => c.Name == "OS");
    }

    private string CreateMinimalTar()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.tar");
        var data = new byte[1024]; // 2 blocks, second = zero terminator
        // First header
        var name = "hello.txt\0".ToCharArray();
        for (int i = 0; i < name.Length; i++) data[i] = (byte)name[i];
        var size = "       11\0".ToCharArray(); // 11 = 9 octal + null
        for (int i = 0; i < size.Length; i++) data[124 + i] = (byte)size[i];
        // ustar magic at offset 257
        data[257] = (byte)'u'; data[258] = (byte)'s'; data[259] = (byte)'t';
        data[260] = (byte)'a'; data[261] = (byte)'r'; data[262] = (byte)' ';
        // checksum (8 spaces to satisfy simple checkers)
        for (int i = 0; i < 8; i++) data[148 + i] = (byte)' ';

        File.WriteAllBytes(path, data);
        return path;
    }

    private string CreateMinimalGzip()
    {
        var path = Path.Combine(_testDir, $"{Guid.NewGuid():N}.gz");
        var data = new byte[18];
        data[0] = 0x1F; data[1] = 0x8B; data[2] = 0x08; // magic
        data[3] = 0; // FLG = 0
        data[4] = 0; data[5] = 0; data[6] = 0; data[7] = 0; // MTIME = 0
        data[8] = 0; // XFL = 0
        data[9] = 3; // OS = Unix
        data[10] = 0; data[11] = 0; data[12] = 0; data[13] = 0; // CRC32
        data[14] = 0; data[15] = 0; data[16] = 0; data[17] = 0; // ISIZE

        File.WriteAllBytes(path, data);
        return path;
    }
}