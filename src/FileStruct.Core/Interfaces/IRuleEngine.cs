using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IRuleEngine
{
    Task<List<FormatRule>> LoadRuleLibraryAsync(string filePath);
    Task<List<FormatRule>> LoadRuleDirectoryAsync(string directoryPath);
    bool ValidateRule(FormatRule rule);
    List<RuleConflict> DetectConflicts(FormatRule newRule);
    void ResolveConflict(RuleConflict conflict, ConflictResolutionStrategy strategy);
    void RemoveRule(FormatRule rule);
    void EnableRule(FormatRule rule, bool enabled);
    List<FormatRule> GetActiveRules();
    List<FormatRule> GetAllRules();
}

public class RuleConflict
{
    public FormatRule ExistingRule { get; set; } = null!;
    public FormatRule NewRule { get; set; } = null!;
    public string OverlappingSignature { get; set; } = "";
    public ConflictType Type { get; set; }
}

public enum ConflictType
{
    SignatureOverlap,
    FormatNameConflict,
}

public enum ConflictResolutionStrategy
{
    KeepExisting,
    ReplaceWithNew,
    KeepBoth,
    Skip,
}
