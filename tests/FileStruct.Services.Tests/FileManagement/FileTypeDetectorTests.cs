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

    #region Helpers

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
}
