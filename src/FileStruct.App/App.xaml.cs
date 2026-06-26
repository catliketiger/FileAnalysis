using System.Text;
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

        // 加载内置规则
        var ruleLoader = new BuiltinRuleLoader();
        var builtinRules = ruleLoader.LoadAll();
        logService.Info($"已加载 {builtinRules.Count} 个内置格式规则");

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
}
