using FileStruct.Core.Models;

namespace FileStruct.Services.RuleEngine;

public class RuleValidator
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "uint8", "int8", "uint16", "int16", "uint32", "int32", "uint64", "int64",
        "float", "double", "ascii", "utf8", "utf16", "bytes", "struct", "array", "padding",
    };

    public bool Validate(FormatRule rule, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Format))
            errors.Add("规则缺少 'format' 字段");

        if (string.IsNullOrWhiteSpace(rule.RuleVersion))
            errors.Add("规则缺少 'ruleVersion' 字段");

        foreach (var sig in rule.Signatures)
        {
            if (string.IsNullOrWhiteSpace(sig.Name))
                errors.Add("签名缺少 'name' 字段");
            if (sig.Pattern == null || sig.Pattern.Length == 0)
                errors.Add($"签名 '{sig.Name}' 缺少 'pattern' 字段");
            if (sig.Pattern != null && sig.Pattern.Length > 32)
                errors.Add($"签名 '{sig.Name}' 的 pattern 过长 (最大32字节)");
        }

        foreach (var structure in rule.Structures)
        {
            if (string.IsNullOrWhiteSpace(structure.Name))
                errors.Add("结构定义缺少 'name' 字段");

            foreach (var field in structure.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                    errors.Add($"结构 '{structure.Name}' 中存在缺少名称的字段");
                if (!ValidTypes.Contains(field.Type))
                    errors.Add($"结构 '{structure.Name}' 中的字段 '{field.Name}' 使用了未知类型 '{field.Type}'");
            }
        }

        return errors.Count == 0;
    }
}
