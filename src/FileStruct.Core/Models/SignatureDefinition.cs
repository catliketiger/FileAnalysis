namespace FileStruct.Core.Models;

/// <summary>
/// 签名定义：用于魔数匹配的特征描述
/// </summary>
public readonly struct SignatureDefinition
{
    public SignatureDefinition(string formatName, byte[] magicBytes,
        int magicOffset = 0, int minFileSize = 1,
        string? description = null, bool isUserDefined = false, string? ruleSource = null)
    {
        FormatName = formatName;
        MagicBytes = magicBytes;
        MagicOffset = magicOffset;
        MinFileSize = minFileSize;
        Description = description;
        IsUserDefined = isUserDefined;
        RuleSource = ruleSource;
    }

    public string FormatName { get; }
    public byte[] MagicBytes { get; }
    public int MagicOffset { get; }
    public int MinFileSize { get; }
    public string? Description { get; }
    public bool IsUserDefined { get; }
    public string? RuleSource { get; }
}
