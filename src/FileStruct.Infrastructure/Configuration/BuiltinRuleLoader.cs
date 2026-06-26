using System.Reflection;
using System.Text.Json;
using FileStruct.Core.Models;

namespace FileStruct.Infrastructure.Configuration;

public class BuiltinRuleLoader
{
    public List<FormatRule> LoadAll()
    {
        var rules = new List<FormatRule>();
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null) return rules;

        var names = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);

        foreach (var name in names)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var rule = JsonSerializer.Deserialize<FormatRule>(json);
                if (rule != null && !string.IsNullOrWhiteSpace(rule.Format))
                {
                    rule.SourcePath = $"embed://{name}";
                    rules.Add(rule);
                }
            }
            catch { }
        }
        return rules;
    }
}
