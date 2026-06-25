using FileStruct.Core.Models;
using FileStruct.Services.RuleEngine;

namespace FileStruct.Services.Tests.RuleEngine;

public class RuleValidatorTests
{
    private readonly RuleValidator _validator = new();

    [Fact]
    public void Validate_ValidRule_ReturnsTrue()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "MyFormat",
            Signatures =
            [
                new FormatSignature { Name = "magic", Pattern = [0x89, 0x50] },
            ],
            Structures =
            [
                new FormatStructure
                {
                    Name = "Header",
                    Fields =
                    [
                        new FormatField { Name = "magic", Type = "uint32", Offset = 0 },
                    ],
                },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.True(result);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingFormat_ReturnsErrors()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "",
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("format"));
    }

    [Fact]
    public void Validate_EmptySignaturePattern_ReturnsErrors()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "Test",
            Signatures =
            [
                new FormatSignature { Name = "empty", Pattern = [] },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("pattern"));
    }

    [Fact]
    public void Validate_SignatureNameMissing_ReturnsErrors()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "Test",
            Signatures =
            [
                new FormatSignature { Name = "", Pattern = [0x00] },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validate_UnknownFieldType_ReturnsErrors()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "Test",
            Structures =
            [
                new FormatStructure
                {
                    Name = "Data",
                    Fields =
                    [
                        new FormatField { Name = "x", Type = "nonexistent_type", Offset = 0 },
                    ],
                },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("nonexistent_type"));
    }

    [Fact]
    public void Validate_LongPattern_ReturnsErrors()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "Test",
            Signatures =
            [
                new FormatSignature { Name = "long", Pattern = new byte[33] },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("过长"));
    }

    [Fact]
    public void Validate_MinimalSignatureOnly_ReturnsTrue()
    {
        var rule = new FormatRule
        {
            RuleVersion = "1.0",
            Format = "Minimal",
            Signatures =
            [
                new FormatSignature { Name = "sig", Pattern = [0x00, 0x01] },
            ],
        };

        var result = _validator.Validate(rule, out var errors);

        Assert.True(result);
        Assert.Empty(errors);
    }
}
