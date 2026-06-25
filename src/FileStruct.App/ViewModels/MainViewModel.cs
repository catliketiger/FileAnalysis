using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.App.Controls;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

/// <summary>
/// 主视图模型：管理文件加载、视图切换、项目操作
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IProjectService _projectService;
    private readonly IStructureRecognizer _recognizer;
    private readonly ILogService _logger;
    private BinaryBuffer? _buffer;

    public MainViewModel(IFileService fileService, IProjectService projectService,
        IStructureRecognizer recognizer, ILogService logger)
    {
        _fileService = fileService;
        _projectService = projectService;
        _recognizer = recognizer;
        _logger = logger;
        _logger.Debug("MainViewModel 初始化");
    }

    [ObservableProperty]
    private string _windowTitle = "FileStruct - 二进制文件结构化分析工具";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _fileInfoText = "";

    [ObservableProperty]
    private bool _isFileLoaded;

    [ObservableProperty]
    private string _activeView = "Hex";

    [ObservableProperty]
    private HexEditorViewModel _hexEditor = new();

    [ObservableProperty]
    private TextViewModel _textView = new();

    [ObservableProperty]
    private StructureTreeViewModel _structureTree = new();

    [ObservableProperty]
    private bool _isRecognizing;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开二进制文件",
            Filter = "所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText = "正在加载文件...";

            _buffer?.Dispose();
            _buffer = await _fileService.LoadFileAsync(dialog.FileName);

            // 检测类型
            var fileType = _fileService.DetectFileType(_buffer);
            FileInfoText = $"{_buffer.FileName} | {FormatFileSize(_buffer.Length)} | {fileType.DisplayName}";

            // 分发给子 ViewModel
            HexEditor.Buffer = _buffer;
            HexEditor.FileType = fileType;

            // 判断默认视图
            ActiveView = fileType.IsText ? "Text" : "Hex";

            if (fileType.IsText)
            {
                var encoding = fileType.GetEncoding() ?? System.Text.Encoding.UTF8;
                TextView.LoadText(_buffer, encoding);
            }

            IsFileLoaded = true;
            WindowTitle = $"FileStruct - {_buffer.FileName}";
            StatusText = "文件已加载";
            _logger.Info($"文件已打开: {_buffer.FileName} ({_buffer.Length} 字节)");
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
            _logger.Error($"打开文件失败", ex);
        }
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (_buffer == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存项目",
            DefaultExt = ".fstruct",
            Filter = "FileStruct 项目文件 (*.fstruct)|*.fstruct"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText = "正在保存项目...";
            var project = new ProjectFile
            {
                SourceFile = new SourceFileInfo
                {
                    OriginalPath = _buffer.FilePath,
                    FileName = _buffer.FileName,
                    FileSize = _buffer.Length,
                },
                ViewState = new ViewState
                {
                    ActiveView = ActiveView,
                    ScrollOffset = HexEditor.ScrollOffset,
                    ByteGroupSize = HexEditor.ByteGroupSize,
                },
            };
            await _projectService.SaveAsync(project, dialog.FileName);
            StatusText = "项目已保存";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败: {ex.Message}";
            _logger.Error($"保存项目失败", ex);
        }
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开项目",
            DefaultExt = ".fstruct",
            Filter = "FileStruct 项目文件 (*.fstruct)|*.fstruct"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText = "正在打开项目...";
            var project = await _projectService.OpenAsync(dialog.FileName);

            // 检查源文件是否存在
            if (!string.IsNullOrEmpty(project.SourceFile.OriginalPath) &&
                File.Exists(project.SourceFile.OriginalPath))
            {
                // 检查哈希
                if (!_projectService.VerifySourceFileHash(project, project.SourceFile.OriginalPath))
                {
                    StatusText = "警告：源文件已被修改";
                }

                _buffer?.Dispose();
                _buffer = await _fileService.LoadFileAsync(project.SourceFile.OriginalPath);
                HexEditor.Buffer = _buffer;

                // 恢复视图状态
                if (project.ViewState != null)
                {
                    ActiveView = project.ViewState.ActiveView;
                    HexEditor.ScrollOffset = project.ViewState.ScrollOffset;
                    HexEditor.ByteGroupSize = project.ViewState.ByteGroupSize;
                }

                IsFileLoaded = true;
                WindowTitle = $"FileStruct - {project.SourceFile.FileName}";
                StatusText = "项目已打开";
            }
            else
            {
                StatusText = $"源文件未找到: {project.SourceFile.OriginalPath}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"打开项目失败: {ex.Message}";
            _logger.Error($"打开项目失败", ex);
        }
    }

    [RelayCommand]
    private async Task RecognizeAsync()
    {
        if (_buffer == null) return;

        try
        {
            IsRecognizing = true;
            StatusText = "正在进行结构识别...";

            var result = await _recognizer.RecognizeAsync(_buffer,
                new Progress<RecognitionProgress>(p =>
                {
                    StatusText = p.StatusText;
                }));

            StructureTree.LoadTree(result);
            StatusText = $"结构识别完成，共发现 {CountNodes(result)} 个字段";
            _logger.Info($"结构识别完成: {CountNodes(result)} 个节点");
        }
        catch (Exception ex)
        {
            StatusText = $"识别失败: {ex.Message}";
            _logger.Error("结构识别失败", ex);
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private static int CountNodes(StructureNode node)
    {
        return 1 + node.Children.Sum(CountNodes);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
