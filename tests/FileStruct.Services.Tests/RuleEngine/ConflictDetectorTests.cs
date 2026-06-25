using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using FileStruct.Services.RuleEngine;

namespace FileStruct.Services.Tests.RuleEngine;

public class ConflictDetectorTests
{
    private readonly ConflictDetector _detector = new();

    [Fact]
    public void DetectConflicts_NoOverlap_ReturnsEmpty()
    {
        var existing = new FormatRule
        {
            Format = "PNG",
            Signatures =
            [
                new FormatSignature { Name = "png-magic", Pattern = [0x89, 0x50, 0x4E, 0x47] },
            ],
        };

        var newRule = new FormatRule
        {
            Format = "ZIP",
            Signatures =
            [
                new FormatSignature { Name = "zip-magic", Pattern = [0x50, 0x4B, 0x03, 0x04] },
            ],
        };

        var conflicts = _detector.DetectConflicts(newRule, [existing]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_SameSignature_ReturnsConflict()
    {
        var existing = new FormatRule
        {
            Format = "Existing",
            Signatures =
            [
                new FormatSignature { Name = "magic", Pattern = [0x89, 0x50] },
            ],
        };

        var newRule = new FormatRule
        {
            Format = "New",
            Signatures =
            [
                new FormatSignature { Name = "magic2", Pattern = [0x89, 0x50] },
            ],
        };

        var conflicts = _detector.DetectConflicts(newRule, [existing]);

        Assert.NotEmpty(conflicts);
        Assert.Equal(ConflictType.SignatureOverlap, conflicts[0].Type);
    }

    [Fact]
    public void DetectConflicts_SameFormatName_ReturnsConflict()
    {
        var existing = new FormatRule { Format = "MyFormat" };
        var newRule = new FormatRule { Format = "myformat" }; // case-insensitive

        var conflicts = _detector.DetectConflicts(newRule, [existing]);

        Assert.NotEmpty(conflicts);
        Assert.Equal(ConflictType.FormatNameConflict, conflicts[0].Type);
    }

    [Fact]
    public void DetectConflicts_DisabledRule_Ignored()
    {
        var existing = new FormatRule
        {
            Format = "Existing",
            IsEnabled = false,
            Signatures =
            [
                new FormatSignature { Name = "magic", Pattern = [0x89, 0x50] },
            ],
        };

        var newRule = new FormatRule
        {
            Format = "New",
            Signatures =
            [
                new FormatSignature { Name = "magic", Pattern = [0x89, 0x50] },
            ],
        };

        var conflicts = _detector.DetectConflicts(newRule, [existing]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_MultipleConflicts_ReturnsAll()
    {
        var existing = new FormatRule
        {
            Format = "Existing",
            Signatures =
            [
                new FormatSignature { Name = "a", Pattern = [0x00, 0x01] },
                new FormatSignature { Name = "b", Pattern = [0x02, 0x03] },
            ],
        };

        var newRule = new FormatRule
        {
            Format = "Existing",
            Signatures =
            [
                new FormatSignature { Name = "c", Pattern = [0x00, 0x01] },
            ],
        };

        var conflicts = _detector.DetectConflicts(newRule, [existing]);

        Assert.Equal(2, conflicts.Count); // 1 signature overlap + 1 name conflict
    }
}
