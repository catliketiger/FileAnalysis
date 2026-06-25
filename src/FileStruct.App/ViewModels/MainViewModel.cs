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
    private int _selectedTabIndex;

    [ObservableProperty]
    private HexEditorViewModel _hexEditor = new();

    [ObservableProperty]
    private TextViewModel _textView = new();

    [ObservableProperty]
    private StructureTreeViewModel _structureTree = new();

    [ObservableProperty]
    private LivePreviewViewModel _livePreview = new();

    [ObservableProperty]
    private AuxToolsViewModel _auxTools = new();

    [ObservableProperty]
    private BookmarkViewModel _bookmarkList = new();

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
            SelectedTabIndex = fileType.IsText ? 1 : 0;

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
    private void Exit()
    {
        CloseFile();
        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var message = $"FileStruct - 二进制文件结构化分析工具\n" +
                      $"版本: {typeof(MainViewModel).Assembly.GetName().Version}\n" +
                      $"技术栈: .NET 10 + WPF + CommunityToolkit.Mvvm\n\n" +
                      $"功能:\n" +
                      $"• 十六进制/文本双视图\n" +
                      $"• 双引擎结构识别 (魔数匹配 + 启发式推断)\n" +
                      $"• 结构树 + 视图联动\n" +
                      $"• 手动编辑 + 撤销/重做\n" +
                      $"• 自定义格式规则 (JSON/YAML)\n" +
                      $"• 实时解码预览\n" +
                      $"• 项目保存/打开 (SHA256 校验)";

        System.Windows.MessageBox.Show(message, "关于 FileStruct",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void CloseFile()
    {
        if (_buffer == null) return;

        var fileName = _buffer.FileName;
        _buffer.Dispose();
        _buffer = null;

        // 清除各视图
        HexEditor.Buffer = null;
        HexEditor.FileType = null;
        TextView.Clear();
        StructureTree.Clear();
        LivePreview.Clear();

        IsFileLoaded = false;
        SelectedTabIndex = 0;
        WindowTitle = "FileStruct - 二进制文件结构化分析工具";
        FileInfoText = "";
        StatusText = "文件已关闭";
        _logger.Info($"文件已关闭: {fileName}");
    }

    [ObservableProperty]
    private string _gotoOffsetText = "";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _searchResultText = "";

    [RelayCommand]
    private void GoToOffset()
    {
        if (_buffer == null || string.IsNullOrWhiteSpace(GotoOffsetText)) return;

        try
        {
            var offset = GotoOffsetText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(GotoOffsetText, 16)
                : long.Parse(GotoOffsetText);

            if (offset < 0 || offset >= _buffer.Length)
            {
                StatusText = $"偏移越界: 0x{offset:X} (文件大小: 0x{_buffer.Length:X})";
                return;
            }

            HexEditor.ScrollOffset = (offset / 16) * 16;
            HexEditor.SelectionInfo = $"跳转到: 0x{offset:X}";
            StatusText = $"已跳转到偏移 0x{offset:X}";
            GotoOffsetText = "";
        }
        catch (FormatException)
        {
            StatusText = $"无效的偏移格式: {GotoOffsetText} (支持十进制或 0x 十六进制)";
        }
    }

    [RelayCommand]
    private void CopyHex()
    {
        if (_buffer == null || HexEditor.SelectionStart < 0 || HexEditor.SelectionLength <= 0) return;
        var bytes = _buffer.ReadBytes(HexEditor.SelectionStart, (int)Math.Min(HexEditor.SelectionLength, 1024 * 1024));
        var hex = BitConverter.ToString(bytes).Replace("-", " ");
        System.Windows.Clipboard.SetText(hex);
        StatusText = $"已复制 {bytes.Length} 字节的十六进制值到剪贴板";
    }

    [RelayCommand]
    private void CopyAscii()
    {
        if (_buffer == null || HexEditor.SelectionStart < 0 || HexEditor.SelectionLength <= 0) return;
        var bytes = _buffer.ReadBytes(HexEditor.SelectionStart, (int)Math.Min(HexEditor.SelectionLength, 1024 * 1024));
        var ascii = new string(bytes.Select(b => b >= 0x20 && b <= 0x7E ? (char)b : '.').ToArray());
        System.Windows.Clipboard.SetText(ascii);
        StatusText = $"已复制 {ascii.Length} 字节的 ASCII 值到剪贴板";
    }

    [RelayCommand]
    private void AddBookmark()
    {
        if (_buffer == null || HexEditor.SelectionStart < 0) return;
        var name = $"偏移 0x{HexEditor.SelectionStart:X}";
        BookmarkList.AddBookmark(name, HexEditor.SelectionStart);
        StatusText = $"已添加书签: {name}";
    }

    private List<long> _searchResults = [];
    private int _searchResultIndex = -1;

    [RelayCommand]
    private void SearchBytes()
    {
        if (_buffer == null || string.IsNullOrWhiteSpace(SearchText)) return;

        // 解析搜索模式：支持十六进制 ("48 65 6C") 或 ASCII 文本 (用引号包裹)
        byte[] pattern;
        if (SearchText.StartsWith('"') && SearchText.EndsWith('"'))
        {
            var text = SearchText[1..^1];
            pattern = System.Text.Encoding.ASCII.GetBytes(text);
        }
        else
        {
            var hex = SearchText.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0) { SearchResultText = "无效的十六进制序列"; return; }
            pattern = Convert.FromHexString(hex);
        }

        if (pattern.Length == 0) { SearchResultText = "搜索内容为空"; return; }

        // 滑动窗口搜索
        _searchResults.Clear();
        _searchResultIndex = -1;

        var buffer = _buffer;
        for (long offset = 0; offset <= buffer.Length - pattern.Length; offset++)
        {
            var match = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (buffer.ReadByte(offset + i) != pattern[i])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                _searchResults.Add(offset);
                // 限制结果数量防止卡顿
                if (_searchResults.Count >= 1000) break;
            }
        }

        if (_searchResults.Count == 0)
        {
            SearchResultText = "未找到匹配";
            return;
        }

        _searchResultIndex = 0;
        JumpToSearchResult();
    }

    [RelayCommand]
    private void NextSearchResult()
    {
        if (_searchResults.Count == 0) return;
        _searchResultIndex = (_searchResultIndex + 1) % _searchResults.Count;
        JumpToSearchResult();
    }

    private void JumpToSearchResult()
    {
        if (_searchResultIndex < 0 || _searchResultIndex >= _searchResults.Count) return;
        var offset = _searchResults[_searchResultIndex];
        HexEditor.ScrollOffset = (offset / 16) * 16;
        SearchResultText = $"结果 {_searchResultIndex + 1}/{_searchResults.Count} @ 0x{offset:X}";
        StatusText = SearchResultText;
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
