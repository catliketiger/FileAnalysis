namespace FileStruct.Core.Models;

/// <summary>
/// 签名匹配结果
/// </summary>
public readonly struct SignatureMatch
{
    public SignatureMatch(SignatureDefinition definition, int matchOffset,
        bool isFullMatch, double score)
    {
        Definition = definition;
        MatchOffset = matchOffset;
        IsFullMatch = isFullMatch;
        Score = score;
    }

    public SignatureDefinition Definition { get; }
    public int MatchOffset { get; }
    public bool IsFullMatch { get; }
    public double Score { get; }
}
