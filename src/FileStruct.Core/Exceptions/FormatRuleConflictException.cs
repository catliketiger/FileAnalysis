namespace FileStruct.Core.Exceptions;

public class FormatRuleConflictException : FileStructException
{
    public string RuleName { get; }
    public string ExistingRuleName { get; }

    public FormatRuleConflictException(string ruleName, string existingRuleName, string detail)
        : base($"规则冲突: '{ruleName}' 与 '{existingRuleName}' 冲突 - {detail}")
    {
        RuleName = ruleName;
        ExistingRuleName = existingRuleName;
    }
}
