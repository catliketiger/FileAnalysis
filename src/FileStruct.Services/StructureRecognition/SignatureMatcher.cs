using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.StructureRecognition;

public class SignatureMatcher : ISignatureMatcher
{
    private readonly List<SignatureDefinition> _builtinSignatures;
    private readonly List<SignatureDefinition> _userRules = new();

    public SignatureMatcher()
    {
        _builtinSignatures = BuiltinSignatures.GetAll();
    }

    public List<SignatureMatch> Match(byte[] headerBytes)
    {
        var results = new List<SignatureMatch>();

        // 用户规则优先
        foreach (var sig in _userRules)
        {
            var match = TryMatch(sig, headerBytes);
            if (match != null) results.Add(match.Value);
        }

        // 内置签名
        foreach (var sig in _builtinSignatures)
        {
            // 如果已有用户规则匹配到同一格式，跳过内置
            if (results.Any(r => r.Definition.FormatName == sig.FormatName && r.Definition.IsUserDefined))
                continue;

            var match = TryMatch(sig, headerBytes);
            if (match != null) results.Add(match.Value);
        }

        return results.OrderByDescending(r => r.Score).ThenByDescending(r => r.Definition.MagicBytes.Length).ToList();
    }

    public void AddUserRule(SignatureDefinition rule)
    {
        _userRules.Add(rule);
    }

    public void ClearUserRules()
    {
        _userRules.Clear();
    }

    private static SignatureMatch? TryMatch(SignatureDefinition sig, byte[] headerBytes)
    {
        var offset = sig.MagicOffset;
        var magic = sig.MagicBytes;

        if (offset + magic.Length > headerBytes.Length)
            return null;

        if (headerBytes.Length < sig.MinFileSize)
            return null;

        bool match = true;
        for (int i = 0; i < magic.Length; i++)
        {
            if (headerBytes[offset + i] != magic[i])
            {
                match = false;
                break;
            }
        }

        if (!match) return null;

        // 分数基于魔数长度占比和是否用户定义
        var score = sig.IsUserDefined ? 1.0 : Math.Min(1.0, magic.Length / 8.0);
        return new SignatureMatch(sig, offset, true, score);
    }
}
