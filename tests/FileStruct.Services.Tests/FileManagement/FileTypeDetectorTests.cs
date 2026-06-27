using FileStruct.Core.Models;
using FileStruct.Services.FileManagement;

namespace FileStruct.Services.Tests.FileManagement;

public class FileTypeDetectorTests
{
    private readonly FileTypeDetector _detector = new();
    private readonly string _testDir;

    public FileTypeDetectorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileStructTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    #region DetectByExtension

    [Fact]
    public void DetectByExtension_TxtFile_ReturnsText()
    {
        var result = _detector.DetectByExtension("readme.txt");

        Assert.True(result.IsText);
        Assert.Equal(".txt", result.Extension);
    }

    [Fact]
    public void DetectByExtension_JsonFile_ReturnsText()
    {
        var result = _detector.DetectByExtension("config.json");

        Assert.True(result.IsText);
    }

    [Fact]
    public void DetectByExtension_BinFile_ReturnsBinary()
    {
        var result = _detector.DetectByExtension("data.bin");

        Assert.False(result.IsText);
        Assert.Equal(".bin", result.Extension);
    }

    [Fact]
    public void DetectByExtension_NoExtension_ReturnsUnknown()
    {
        var result = _detector.DetectByExtension("README");

        Assert.Equal(FileStruct.Core.Models.FileCategory.Unknown, result.Category);
    }

    [Fact]
    public void DetectByExtension_PngFile_ReturnsImage()
    {
        var result = _detector.DetectByExtension("image.png");

        Assert.Equal(FileStruct.Core.Models.FileCategory.Image, result.Category);
    }

    #endregion

    #region DetectByHeader

    [Fact]
    public void DetectByHeader_PngMagic_ReturnsImage()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

        var result = _detector.DetectByHeader(header);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Image, result.Category);
        Assert.Equal("PNG 图片", result.DisplayName);
    }

    [Fact]
    public void DetectByHeader_ZipMagic_ReturnsArchive()
    {
        byte[] header = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00];

        var result = _detector.DetectByHeader(header);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Archive, result.Category);
    }

    [Fact]
    public void DetectByHeader_ExeMagic_ReturnsExecutable()
    {
        byte[] header = [0x4D, 0x5A, 0x90, 0x00];

        var result = _detector.DetectByHeader(header);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Executable, result.Category);
    }

    [Fact]
    public void DetectByHeader_UnknownBytes_ReturnsBinary()
    {
        byte[] header = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03];

        var result = _detector.DetectByHeader(header);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Binary, result.Category);
    }

    [Fact]
    public void DetectByHeader_ShortHeader_DoesntThrow()
    {
        byte[] header = [0x89, 0x50]; // Too short for PNG magic

        var result = _detector.DetectByHeader(header);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Binary, result.Category);
    }

    #endregion

    #region Detect (combined)

    [Fact]
    public void Detect_TextFileByExtensionAndHeader_ReturnsText()
    {
        var path = CreateTestFile("test.txt", "Hello World");

        var result = _detector.Detect(path, [0x48, 0x65, 0x6C, 0x6C, 0x6F]);

        Assert.True(result.IsText);
    }

    [Fact]
    public void Detect_PngNoExtension_DetectedByHeader()
    {
        var path = CreateTestFile("unknown", []);
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var result = _detector.Detect(path, pngHeader);

        Assert.Equal(FileStruct.Core.Models.FileCategory.Image, result.Category);
    }

    #endregion

    #region APK detection

    [Fact]
    public void Detect_ApkFileWithZipHeader_ReturnsApk()
    {
        // ZIP 魔数 + .apk 扩展名 → Android APK
        var path = CreateTestFile("test.apk", [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00]);

        var result = _detector.Detect(path, [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00]);

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("APK", result.DisplayName, StringComparison.Ordinal);
        Assert.Equal(".apk", result.Extension);
    }

    [Fact]
    public void Detect_ZipFileWithZipHeader_StillReturnsArchive()
    {
        // 回归测试：.zip 文件仍为 Archive/ZIP
        var path = CreateTestFile("archive.zip", [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00]);
        var result = _detector.Detect(path, [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00]);

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Equal("ZIP 压缩包", result.DisplayName);
    }

    [Fact]
    public void Detect_ApkFileWithoutZipHeader_FallsBackToBinary()
    {
        // 无 ZIP 魔数时现有流程返回 Binary（扩展名注册未被使用）
        var path = CreateTestFile("test.apk", [0x00, 0x00, 0x00, 0x00]);
        var result = _detector.Detect(path, [0x00, 0x00, 0x00, 0x00]);

        Assert.Equal(FileCategory.Binary, result.Category);
    }

    #endregion

    #region Ebook detection

    [Fact]
    public void Detect_EpubFileWithZipHeader_ReturnsDocument()
    {
        // ZIP 魔数 + .epub 扩展名 → EPUB 电子书
        var path = CreateTestFile("test.epub", [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00]);
        var result = _detector.Detect(path, [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00]);

        Assert.Equal(FileCategory.Document, result.Category);
        Assert.Contains("EPUB", result.DisplayName, StringComparison.Ordinal);
        Assert.Equal(".epub", result.Extension);
    }

    [Fact]
    public void Detect_MobiFileWithBookMobiMagic_ReturnsDocument()
    {
        // BOOKMOBI (8 字节) 在偏移 0x3C 处 → MOBI 电子书
        var path = CreateTestFile("test.mobi", CreateMobiHeaderBytes());
        var result = _detector.Detect(path, CreateMobiHeaderBytes());

        Assert.Equal(FileCategory.Document, result.Category);
        Assert.Contains("MOBI", result.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_ZipFileRenameToEpub_DetectsAsEpub()
    {
        // .epub 文件即使实质为 ZIP，也显示为 EPUB 电子书
        var path = CreateTestFile("test.epub", [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00]);
        var result = _detector.Detect(path, [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00]);

        Assert.Equal(FileCategory.Document, result.Category);
        Assert.Contains("EPUB", result.DisplayName);
    }

    #endregion

    #region CRX detection

    [Fact]
    public void Detect_CrxFileWithCr24Magic_ReturnsArchive()
    {
        // Cr24 魔数 + .crx 扩展名 → Chrome 扩展包
        var path = CreateTestFile("test.crx", CreateCrxV2HeaderBytes());
        var result = _detector.Detect(path, CreateCrxV2HeaderBytes());

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("Chrome", result.DisplayName, StringComparison.Ordinal);
        Assert.Equal(".crx", result.Extension);
    }

    #endregion

    #region PAK detection

    [Fact]
    public void Detect_PakFileWithPACKMagic_ReturnsArchive()
    {
        // "PACK" 魔数 → Unreal Engine 资源包
        var path = CreateTestFile("test.pak", [0x50, 0x41, 0x43, 0x4B, 0x08, 0x00, 0x00, 0x00]);
        var result = _detector.Detect(path, [0x50, 0x41, 0x43, 0x4B, 0x08, 0x00, 0x00, 0x00]);

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("Unreal", result.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(".pak", result.Extension);
    }

    #endregion

    #region CAB detection

    [Fact]
    public void Detect_CabFileWithMSCFMagic_ReturnsArchive()
    {
        // "MSCF" 魔数 → Cabinet 压缩包
        var path = CreateTestFile("test.cab", [0x4D, 0x53, 0x43, 0x46, 0x00, 0x00, 0x00, 0x00]);
        var result = _detector.Detect(path, [0x4D, 0x53, 0x43, 0x46, 0x00, 0x00, 0x00, 0x00]);

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("CAB", result.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 7z detection

    [Fact]
    public void Detect_7zFile_ReturnsArchive()
    {
        var path = CreateTestFile("test.7z", [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]);
        var result = _detector.Detect(path, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]);

        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("7z", result.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region TAR + GZip detection

    [Fact]
    public void Detect_TarFileWithUstar_ReturnsArchive()
    {
        var data = new byte[520];
        data[0] = (byte)'t'; data[1] = (byte)'e'; data[2] = (byte)'s'; data[3] = (byte)'t'; // filename
        data[257] = (byte)'u'; data[258] = (byte)'s'; data[259] = (byte)'t';
        data[260] = (byte)'a'; data[261] = (byte)'r'; // "ustar" at offset 257
        var path = CreateTestFile("test.tar", data);
        var result = _detector.Detect(path, data);
        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("TAR", result.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detect_GzipFile_ReturnsArchive()
    {
        // GZip magic: 0x1F 0x8B 0x08
        var data = new byte[20];
        data[0] = 0x1F; data[1] = 0x8B; data[2] = 0x08;
        var path = CreateTestFile("test.gz", data);
        var result = _detector.Detect(path, data);
        Assert.Equal(FileCategory.Archive, result.Category);
        Assert.Contains("GZip", result.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Helpers

    /// <summary>创建最小 CRX v2 头部（Cr24 + version=2 + pubKeyLen=0 + sigLen=0）</summary>
    private static byte[] CreateCrxV2HeaderBytes()
    {
        // CRX v2 头部固定 16 字节: Cr24(4) + version(4) + pubKeyLen(4) + sigLen(4)
        return [0x43, 0x72, 0x32, 0x34, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    }

    /// <summary>创建包含 BOOKMOBI 魔数的测试文件字节（最少 128 字节）</summary>
    private static byte[] CreateMobiHeaderBytes()
    {
        var data = new byte[128];
        // BOOKMOBI 在偏移 0x3C 处
        data[0x3C] = (byte)'B'; data[0x3D] = (byte)'O';
        data[0x3E] = (byte)'O'; data[0x3F] = (byte)'K';
        data[0x40] = (byte)'M'; data[0x41] = (byte)'O';
        data[0x42] = (byte)'B'; data[0x43] = (byte)'I';
        return data;
    }

    private string CreateTestFile(string name, string content)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateTestFile(string name, byte[] content)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    #endregion

    #region Volume discovery

    [Fact]
    public void DiscoverVolumes_ZipWithSplits_DetectsAllVolumes()
    {
        // 创建 .zip + .z01 + .z02
        var baseDir = Path.Combine(Path.GetTempPath(), "VolTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        try
        {
            var zipPath = Path.Combine(baseDir, "test.zip");
            var z01Path = Path.Combine(baseDir, "test.z01");
            var z02Path = Path.Combine(baseDir, "test.z02");
            File.WriteAllBytes(zipPath, new byte[100]);
            File.WriteAllBytes(z01Path, new byte[100]);
            File.WriteAllBytes(z02Path, new byte[100]);

            var vol = FileStruct.App.Utils.VolumeHelper.DiscoverVolumes(zipPath);
            Assert.True(vol.IsMultiVolume);
            Assert.Equal(3, vol.Volumes.Count);
        }
        finally { Directory.Delete(baseDir, true); }
    }

    [Fact]
    public void DiscoverVolumes_SingleZip_NotMultiVolume()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "VolTest2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        try
        {
            var path = Path.Combine(baseDir, "single.zip");
            File.WriteAllBytes(path, new byte[100]);
            var vol = FileStruct.App.Utils.VolumeHelper.DiscoverVolumes(path);
            Assert.False(vol.IsMultiVolume);
            Assert.Single(vol.Volumes);
        }
        finally { Directory.Delete(baseDir, true); }
    }

    #endregion
}
