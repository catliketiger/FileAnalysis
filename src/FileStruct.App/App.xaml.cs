using System.Windows;
using FileStruct.Core.Interfaces;
using FileStruct.Infrastructure.Configuration;
using FileStruct.Infrastructure.Logging;
using FileStruct.Services.Configuration;
using FileStruct.Services.FileManagement;
using FileStruct.Services.ProjectManagement;
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
