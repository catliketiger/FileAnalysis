using FileStruct.Core.Models;
using FileStruct.Services.ProjectManagement;

namespace FileStruct.Services.Tests.ProjectManagement;

public class ProjectSerializerTests
{
    private readonly ProjectSerializer _serializer = new();

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesAllData()
    {
        var project = new ProjectFile
        {
            Version = "0.1.0",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SourceFile = new SourceFileInfo
            {
                OriginalPath = @"C:\test\file.bin",
                FileName = "file.bin",
                FileSize = 1024,
                Sha256Hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            },
            ViewState = new ViewState
            {
                ActiveView = "Hex",
                ScrollOffset = 256,
                ByteGroupSize = 4,
                IsLittleEndian = false,
            },
            Bookmarks =
            [
                new Bookmark
                {
                    Name = "Header",
                    Offset = 0,
                    Description = "File header start",
                },
                new Bookmark
                {
                    Name = "Footer",
                    Offset = 1000,
                    Color = "#FFFF0000",
                },
            ],
            Notes =
            [
                new UserNote
                {
                    Offset = 50,
                    Length = 10,
                    Content = "This is a test note",
                },
            ],
        };

        // Act
        var json = _serializer.Serialize(project);
        var deserialized = _serializer.Deserialize(json);

        // Assert
        Assert.Equal(project.Version, deserialized.Version);
        Assert.Equal(project.SourceFile.FileName, deserialized.SourceFile.FileName);
        Assert.Equal(project.SourceFile.FileSize, deserialized.SourceFile.FileSize);
        Assert.Equal(project.SourceFile.Sha256Hash, deserialized.SourceFile.Sha256Hash);
        Assert.Equal(project.ViewState.ActiveView, deserialized.ViewState.ActiveView);
        Assert.Equal(project.ViewState.ScrollOffset, deserialized.ViewState.ScrollOffset);
        Assert.Equal(project.ViewState.ByteGroupSize, deserialized.ViewState.ByteGroupSize);
        Assert.Equal(project.ViewState.IsLittleEndian, deserialized.ViewState.IsLittleEndian);
        Assert.Equal(project.Bookmarks.Count, deserialized.Bookmarks.Count);
        Assert.Equal(project.Bookmarks[0].Name, deserialized.Bookmarks[0].Name);
        Assert.Equal(project.Bookmarks[0].Offset, deserialized.Bookmarks[0].Offset);
        Assert.Equal(project.Notes.Count, deserialized.Notes.Count);
        Assert.Equal(project.Notes[0].Content, deserialized.Notes[0].Content);
    }

    [Fact]
    public void Serialize_MinimalProject_DoesNotThrow()
    {
        var project = new ProjectFile();
        var json = _serializer.Serialize(project);
        Assert.NotNull(json);
        Assert.NotEmpty(json);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsException()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            _serializer.Deserialize("{invalid json}"));
    }

    [Fact]
    public void Deserialize_NullJson_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _serializer.Deserialize(null!));
    }

    [Fact]
    public async Task ComputeHashAsync_ValidFile_ReturnsHexString()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, "Hello World"u8.ToArray());

            var hash = await _serializer.ComputeHashAsync(path);

            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length); // SHA256 hex = 64 chars
            Assert.Matches("^[a-f0-9]{64}$", hash);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
