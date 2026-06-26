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
        UserAction = "请检查导入的规则文件是否与现有规则重复，或手动选择冲突解决方案（保留/替换/跳过）";
        ContextInfo["NewRule"] = ruleName;
        ContextInfo["ExistingRule"] = existingRuleName;
        ContextInfo["Detail"] = detail;
    }
}
