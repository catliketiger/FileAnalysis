using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using FileStruct.Core.Interfaces;
using FileStruct.Infrastructure.Configuration;
using FileStruct.Infrastructure.Logging;
using FileStruct.Services.Configuration;
using FileStruct.Services.FileManagement;
using FileStruct.Services.ProjectManagement;
using FileStruct.Services.StructureRecognition;
using FileStruct.Services.EditService;
using FileStruct.Services.RuleEngine;
using FileStruct.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FileStruct.App;

/// <summary>
/// App 入口：配置 DI 容器，加载配置，初始化日志
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册编码提供程序（支持 GBK/GB2312/Shift-JIS 等）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        base.OnStartup(e);

        var services = new ServiceCollection();

        // 基础设施
        var configStore = new ConfigFileStore();
        var config = configStore.LoadConfig();
        services.AddSingleton(config);
        services.AddSingleton(configStore);

        var logService = new LogService(config);
        services.AddSingleton<ILogService>(logService);

        // 服务层
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ProjectSerializer>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IConfigService, ConfigService>();

        // 加载内置格式规则
        var builtinRules = BuiltinRuleProvider.GetAll();
        logService.Info($"已加载 {builtinRules.Count} 个内置格式规则: {string.Join(", ", builtinRules.Select(r => r.Format))}");

        // DEBUG 模式：导出规则文件到 Rules 目录便于核对
        if (config.Debug.Enabled)
        {
            var rulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Rules");
            ExportRules(builtinRules, rulesDir, logService);
        }

        // V1.0 规则引擎 + 识别引擎
        services.AddSingleton<IRuleEngine, RuleEngine>(sp =>
        {
            var engine = new RuleEngine(sp.GetRequiredService<ILogService>());
            foreach (var rule in builtinRules)
                engine.AddBuiltinRule(rule);
            return engine;
        });

        services.AddSingleton<ISignatureMatcher, SignatureMatcher>();
        services.AddSingleton<IHeuristicEngine, HeuristicEngine>();
        services.AddSingleton<IConfidenceScorer, ConfidenceScorer>();
        services.AddSingleton<IStructureRecognizer, StructureRecognizer>();

        // V1.0 编辑服务
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddSingleton<IEditService, EditService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        Services = services.BuildServiceProvider();

        // 启动主窗口
        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    /// <summary>将内置规则导出为 JSON 文件到指定目录</summary>
    private static void ExportRules(List<FileStruct.Core.Models.FormatRule> rules, string dir, ILogService log)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            foreach (var rule in rules)
            {
                var fileName = $"{rule.Format.ToLowerInvariant().Replace(" ", "-").Replace("/", "-")}.json";
                if (fileName == "midi-track.json") continue;
                var path = Path.Combine(dir, fileName);
                var json = JsonSerializer.Serialize(rule, opts);
                File.WriteAllText(path, json);
                log.Debug($"规则导出: {path}");
            }
            log.Info($"规则文件已导出到: {dir}");
        }
        catch (Exception ex)
        {
            log.Error($"规则导出失败", ex);
        }
    }
}
