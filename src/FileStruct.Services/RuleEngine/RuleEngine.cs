using System.Text.Json;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.RuleEngine;

public class RuleEngine : IRuleEngine
{
    private readonly List<FormatRule> _rules = new();
    private readonly RuleValidator _validator = new();
    private readonly ConflictDetector _conflictDetector = new();
    private readonly ILogService _logger;

    public RuleEngine(ILogService logger)
    {
        _logger = logger;
    }

    public Task<List<FormatRule>> LoadRuleLibraryAsync(string filePath)
    {
        using var op = _logger.BeginOperation($"加载规则文件: {filePath}");
        _logger.Debug($"加载规则文件: {filePath}");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"规则文件未找到: {filePath}");

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        FormatRule? rule;

        if (extension is ".json")
        {
            var json = File.ReadAllText(filePath);
            rule = JsonSerializer.Deserialize<FormatRule>(json);
        }
        else if (extension is ".yaml" or ".yml")
        {
            var yaml = File.ReadAllText(filePath);
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            rule = deserializer.Deserialize<FormatRule>(yaml);
        }
        else
        {
            throw new NotSupportedException($"不支持的规则文件格式: {extension}");
        }

        if (rule == null)
            throw new InvalidDataException($"规则文件解析失败: {filePath}");

        rule.SourcePath = filePath;

        if (!_validator.Validate(rule, out var errors))
        {
            var errorStr = string.Join("; ", errors);
            _logger.Error($"规则校验失败: {errorStr}");
            throw new InvalidDataException($"规则校验失败: {errorStr}");
        }

        // 检测冲突
        var conflicts = _conflictDetector.DetectConflicts(rule, _rules);
        if (conflicts.Count > 0)
        {
            _logger.Warn($"检测到 {conflicts.Count} 个规则冲突");
        }

        _rules.Add(rule);
        _logger.Info($"规则已加载: {rule.Format} (来自 {filePath})");

        return Task.FromResult(new List<FormatRule> { rule });
    }

    public Task<List<FormatRule>> LoadRuleDirectoryAsync(string directoryPath)
    {
        var results = new List<FormatRule>();
        if (!Directory.Exists(directoryPath)) return Task.FromResult(results);

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                var rules = LoadRuleLibraryAsync(file).GetAwaiter().GetResult();
                results.AddRange(rules);
            }
            catch (Exception ex)
            {
                _logger.Error($"加载规则文件失败: {file}", ex);
            }
        }

        return Task.FromResult(results);
    }

    public bool ValidateRule(FormatRule rule)
    {
        return _validator.Validate(rule, out _);
    }

    public List<RuleConflict> DetectConflicts(FormatRule newRule)
    {
        return _conflictDetector.DetectConflicts(newRule, _rules);
    }

    public void ResolveConflict(RuleConflict conflict, ConflictResolutionStrategy strategy)
    {
        switch (strategy)
        {
            case ConflictResolutionStrategy.KeepExisting:
                _logger.Info($"保留现有规则: {conflict.ExistingRule.Format}");
                break;

            case ConflictResolutionStrategy.ReplaceWithNew:
                _rules.Remove(conflict.ExistingRule);
                _rules.Add(conflict.NewRule);
                _logger.Info($"替换为新的规则: {conflict.NewRule.Format}");
                break;

            case ConflictResolutionStrategy.KeepBoth:
                // 新规则被添加，用户可以通过启用/禁用管理
                _logger.Info($"保留两条规则: {conflict.ExistingRule.Format} + {conflict.NewRule.Format}");
                break;

            case ConflictResolutionStrategy.Skip:
                // 不添加新规则
                _rules.Remove(conflict.NewRule);
                _logger.Info($"跳过冲突规则: {conflict.NewRule.Format}");
                break;
        }
    }

    public void RemoveRule(FormatRule rule)
    {
        _rules.Remove(rule);
        _logger.Info($"规则已移除: {rule.Format}");
    }

    public void EnableRule(FormatRule rule, bool enabled)
    {
        rule.IsEnabled = enabled;
        _logger.Debug($"规则 {(enabled ? "启用" : "禁用")}: {rule.Format}");
    }

    public List<FormatRule> GetActiveRules()
    {
        return _rules.Where(r => r.IsEnabled).ToList();
    }

    public List<FormatRule> GetAllRules()
    {
        return _rules.ToList();
    }
}
