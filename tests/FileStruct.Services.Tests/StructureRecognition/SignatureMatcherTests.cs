using FileStruct.Services.StructureRecognition;

namespace FileStruct.Services.Tests.StructureRecognition;

public class SignatureMatcherTests
{
    private readonly SignatureMatcher _matcher = new();

    [Fact]
    public void Match_PNGHeader_ReturnsPNG()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00];

        var results = _matcher.Match(header);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Definition.FormatName == "PNG");
    }

    [Fact]
    public void Match_ZIPHeader_ReturnsZIP()
    {
        byte[] header = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00];

        var results = _matcher.Match(header);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Definition.FormatName == "ZIP");
    }

    [Fact]
    public void Match_PEHeader_ReturnsPE()
    {
        byte[] header = [0x4D, 0x5A, 0x90, 0x00];

        var results = _matcher.Match(header);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Definition.FormatName == "PE");
    }

    [Fact]
    public void Match_UnknownHeader_ReturnsEmpty()
    {
        byte[] header = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01];

        var results = _matcher.Match(header);

        Assert.Empty(results);
    }

    [Fact]
    public void Match_ShortHeader_NoMatch()
    {
        byte[] header = [0x89, 0x50]; // Too short for PNG

        var results = _matcher.Match(header);

        Assert.Empty(results);
    }

    [Fact]
    public void AddUserRule_UserRuleHasPriority()
    {
        // Add a user rule that matches a known format
        var userRule = new FileStruct.Core.Models.SignatureDefinition(
            "CustomFormat", [0xDE, 0xAD], isUserDefined: true);
        _matcher.AddUserRule(userRule);

        byte[] header = [0xDE, 0xAD, 0xBE, 0xEF];
        var results = _matcher.Match(header);

        Assert.NotEmpty(results);
        var best = results[0];
        Assert.True(best.Definition.IsUserDefined);
        Assert.Equal("CustomFormat", best.Definition.FormatName);
    }

    [Fact]
    public void ClearUserRules_RemovesAllUserRules()
    {
        var userRule = new FileStruct.Core.Models.SignatureDefinition(
            "Test", [0xAA], isUserDefined: true);
        _matcher.AddUserRule(userRule);
        _matcher.ClearUserRules();

        byte[] header = [0xAA];
        var results = _matcher.Match(header);

        // Should not match (built-in signatures don't have 0xAA)
        Assert.Empty(results);
    }

    [Fact]
    public void Match_ResultsOrderedByScoreDescending()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

        var results = _matcher.Match(header);

        if (results.Count >= 2)
        {
            Assert.True(results[0].Score >= results[1].Score);
        }
    }
}
