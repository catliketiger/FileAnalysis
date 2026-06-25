using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.RuleEngine;

public class RuleLibraryManager
{
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogService _logger;
    private readonly string _rulesDirectory;

    public RuleLibraryManager(IRuleEngine ruleEngine, ILogService logger)
    {
        _ruleEngine = ruleEngine;
        _logger = logger;
        _rulesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileStruct", "rules");
        Directory.CreateDirectory(_rulesDirectory);
    }

    public string RulesDirectory => _rulesDirectory;

    public async Task LoadUserRulesAsync()
    {
        if (!Directory.Exists(_rulesDirectory)) return;

        foreach (var file in Directory.GetFiles(_rulesDirectory, "*.json"))
        {
            try
            {
                await _ruleEngine.LoadRuleLibraryAsync(file);
            }
            catch (Exception ex)
            {
                _logger.Error($"加载用户规则失败: {file}", ex);
            }
        }
    }
}
