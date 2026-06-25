using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.RuleEngine;

public class ConflictDetector
{
    public List<RuleConflict> DetectConflicts(FormatRule newRule, List<FormatRule> existingRules)
    {
        var conflicts = new List<RuleConflict>();

        foreach (var existing in existingRules)
        {
            if (!existing.IsEnabled) continue;

            // 检查签名重叠
            foreach (var newSig in newRule.Signatures)
            {
                foreach (var existingSig in existing.Signatures)
                {
                    if (SignaturesOverlap(newSig, existingSig))
                    {
                        conflicts.Add(new RuleConflict
                        {
                            ExistingRule = existing,
                            NewRule = newRule,
                            OverlappingSignature = existingSig.Name,
                            Type = ConflictType.SignatureOverlap,
                        });
                    }
                }
            }

            // 检查格式名冲突
            if (string.Equals(existing.Format, newRule.Format, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(new RuleConflict
                {
                    ExistingRule = existing,
                    NewRule = newRule,
                    OverlappingSignature = existing.Format,
                    Type = ConflictType.FormatNameConflict,
                });
            }
        }

        return conflicts;
    }

    private static bool SignaturesOverlap(FormatSignature a, FormatSignature b)
    {
        if (a.Pattern == null || b.Pattern == null || a.Pattern.Length == 0 || b.Pattern.Length == 0)
            return false;

        // 检查是否一个签名的开头匹配另一个的任意位置
        var offset = Math.Abs(a.Offset - b.Offset);
        var minLen = Math.Min(a.Pattern.Length, b.Pattern.Length);

        if (offset + minLen > Math.Max(a.Pattern.Length, b.Pattern.Length))
            return false;

        for (int i = 0; i < minLen && i + offset < Math.Max(a.Pattern.Length, b.Pattern.Length); i++)
        {
            var aIdx = a.Offset <= b.Offset ? i : i + offset;
            var bIdx = a.Offset <= b.Offset ? i + offset : i;

            if (aIdx >= a.Pattern.Length || bIdx >= b.Pattern.Length)
                break;
            if (a.Pattern[aIdx] != b.Pattern[bIdx])
                return false;
        }

        return true;
    }
}
