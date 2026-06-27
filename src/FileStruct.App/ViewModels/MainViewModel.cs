using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.App.Controls;
using FileStruct.App.Services;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FileStruct.App.ViewModels;

/// <summary>分卷文件列表项</summary>
public partial class VolumeListItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _fullPath = "";

    [ObservableProperty]
    private bool _hasCentralDir;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private int _volumeIndex;

    /// <summary>是否已加载（已识别缓存）</summary>
    public bool IsLoaded { get; set; }

    /// <summary>缓存的识别结果树</summary>
    public StructureNode? CachedRoot { get; set; }
}

/// <summary>
/// 主视图模型：管理文件加载、视图切换、项目操作
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IProjectService _projectService;
    private readonly IStructureRecognizer _recognizer;
    private readonly IEditService _editService;
    private readonly IConfigService _config;
    private readonly ILogService _logger;
    private BinaryBuffer? _buffer;

    // 分卷管理
    private FileStruct.App.Utils.VolumeHelper.VolumeInfo? _currentVolumeInfo;
    private int _currentVolumeIndex;

    /// <summary>分卷文件列表（用于底部面板绑定）</summary>
    public ObservableCollection<VolumeListItem> VolumeList { get; } = new();

    /// <summary>分卷列表面板是否可见</summary>
    [ObservableProperty]
    private bool _isVolumeListVisible;

    /// <summary>ListView 当前选中的分卷项</summary>
    [ObservableProperty]
    private VolumeListItem? _selectedVolumeItem;

    /// <summary>识别操作取消令牌（用于切换去抖）</summary>
    private CancellationTokenSource? _recognitionCts;

    /// <summary>搜索导航守卫：防止树选择覆盖搜索高亮（ViewModel→View 通信用）</summary>
    internal bool IsSearchNavigating { get; set; }

    public MainViewModel(IFileService fileService, IProjectService projectService,
        IStructureRecognizer recognizer, IEditService editService, IConfigService config,
        ILogService logger)
    {
        _fileService = fileService;
        _projectService = projectService;
        _recognizer = recognizer;
        _editService = editService;
        _config = config;
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
    private FileMetaViewModel _fileMeta = new();

    [ObservableProperty]
    private bool _isRecognizing;

    [ObservableProperty]
    private double _recognitionProgress;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开二进制文件",
            Filter = "所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        // 关闭当前文件
        CloseFile();

        try
        {
            StatusText = "正在加载文件...";
            _buffer = await _fileService.LoadFileAsync(dialog.FileName);

            // 检测类型
            var fileType = _fileService.DetectFileType(_buffer);
            FileInfoText = $"{_buffer.FileName} | {FormatFileSize(_buffer.Length)} | {fileType.DisplayName}";

            // 更新文件元数据
            FileMeta.Update(_buffer.FilePath, fileType.DisplayName, _buffer.Length);

            // 分卷检测
            var volInfo = FileStruct.App.Utils.VolumeHelper.DiscoverVolumes(_buffer.FilePath);
            _currentVolumeInfo = null;
            if (volInfo.IsMultiVolume)
            {
                var msg = $"检测到分卷压缩包\n\n{FileStruct.App.Utils.VolumeHelper.GetVolumeSummary(volInfo)}\n\n是否加载所有分卷展开内容？";
                var result = System.Windows.MessageBox.Show(msg, "分卷压缩包", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _currentVolumeInfo = volInfo;
                    _currentVolumeIndex = volInfo.Volumes.FindIndex(v =>
                        string.Equals(v, _buffer.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (_currentVolumeIndex < 0) _currentVolumeIndex = 0;

                    // 填充分卷列表面板
                    VolumeList.Clear();
                    for (int i = 0; i < volInfo.Volumes.Count; i++)
                    {
                        VolumeList.Add(new VolumeListItem
                        {
                            Name = Path.GetFileName(volInfo.Volumes[i]),
                            FullPath = volInfo.Volumes[i],
                            VolumeIndex = i,
                            IsCurrent = (i == _currentVolumeIndex),
                        });
                    }
                    IsVolumeListVisible = true;
                    SelectedVolumeItem = VolumeList.FirstOrDefault(v => v.IsCurrent);

                    StatusText = $"分卷压缩包: {volInfo.Volumes.Count} 卷" +
                        (volInfo.MissingVolumes.Count > 0 ? $" (缺少 {volInfo.MissingVolumes.Count} 卷)" : "");
                }
                else
                {
                    // 用户点"否" → 隐藏分卷列表面板
                    IsVolumeListVisible = false;
                    VolumeList.Clear();
                }
            }
            else
            {
                // 非多卷文件 → 隐藏分卷列表面板
                IsVolumeListVisible = false;
                VolumeList.Clear();
            }

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

            // 自动识别结构（非纯文本文件）
            if (!fileType.IsText)
            {
                StatusText = "正在自动识别结构...";
                await RunRecognitionAsync();
            }
            else
            {
                StatusText = "文件已加载";
            }

            _logger.Info($"文件已打开: {_buffer.FileName} ({_buffer.Length} 字节)");
        }
        catch (Exception ex)
        {
            var userAction = (ex as FileStruct.Core.Exceptions.FileStructException)?.UserAction;
            StatusText = $"加载失败: {ex.Message}";
            if (userAction != null) StatusText += $" — {userAction}";
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
                    DetectedType = HexEditor.FileType,
                },
                ViewState = new ViewState
                {
                    ActiveView = ActiveView,
                    ScrollOffset = HexEditor.ScrollOffset,
                    ByteGroupSize = HexEditor.ByteGroupSize,
                },
                StructureRoot = StructureTree.RootNode != null
                    ? StructureNodeData.FromNode(StructureTree.RootNode) : null,
            };
            await _projectService.SaveAsync(project, dialog.FileName);
            StatusText = "项目已保存";
        }
        catch (Exception ex)
        {
            var userAction = (ex as FileStruct.Core.Exceptions.FileStructException)?.UserAction;
            StatusText = $"保存失败: {ex.Message}";
            if (userAction != null) StatusText += $" — {userAction}";
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

            // 分步诊断
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.Debug("Step 1: 开始反序列化项目文件");
            var project = await _projectService.OpenAsync(dialog.FileName);
            _logger.Debug($"Step 2: 反序列化完成 ({sw.ElapsedMilliseconds}ms)，检查源文件");

            // 检查源文件是否存在
            if (!string.IsNullOrEmpty(project.SourceFile.OriginalPath) &&
                File.Exists(project.SourceFile.OriginalPath))
            {
                _logger.Debug($"Step 3: 源文件存在，检查哈希");
                if (!_projectService.VerifySourceFileHash(project, project.SourceFile.OriginalPath))
                {
                    StatusText = "警告：源文件已被修改";
                }

                _logger.Debug($"Step 4: 加载源文件");
                _buffer?.Dispose();
                _buffer = await _fileService.LoadFileAsync(project.SourceFile.OriginalPath);
                HexEditor.Buffer = _buffer;

                _logger.Debug($"Step 5: 恢复视图状态");
                if (project.ViewState != null)
                {
                    ActiveView = project.ViewState.ActiveView;
                    HexEditor.ScrollOffset = project.ViewState.ScrollOffset;
                    HexEditor.ByteGroupSize = project.ViewState.ByteGroupSize;
                }

                _logger.Debug($"Step 6: 恢复结构树");
                if (project.StructureRoot != null)
                {
                    try
                    {
                        var root = project.StructureRoot.ToNode();
                        StructureTree.LoadTree(root);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("恢复结构树失败", ex);
                        StatusText = "结构树恢复失败，请重新识别";
                    }
                }

                _logger.Debug($"Step 7: 完成 ({sw.ElapsedMilliseconds}ms)");
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
            var userAction = (ex as FileStruct.Core.Exceptions.FileStructException)?.UserAction;
            StatusText = $"打开项目失败: {ex.Message}";
            if (userAction != null) StatusText += $" — {userAction}";
            _logger.Error($"打开项目失败", ex);
        }
    }

    [RelayCommand]
    private void SwitchToHex() => SelectedTabIndex = 0;

    [RelayCommand]
    private void SwitchToText() => SelectedTabIndex = 1;

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
                      $"支持的格式识别:\n" +
                      $"• 可执行文件: PE/ELF/Mach-O/DEB/RPM\n" +
                      $"• 压缩包: ZIP/RAR5/7z/GZip/BZip2/XZ/Zstd/CAB/TAR\n" +
                      $"• 文档: PDF/OLE2/RTF\n" +
                      $"• 图片: PNG/JPEG/GIF/BMP/TIFF/PSD/ICO/WebP\n" +
                      $"• 音频: MP3/FLAC/OGG/MIDI/AIFF\n" +
                      $"• 视频: MP4/AVI/MKV/FLV/MPEG-TS/MOV\n" +
                      $"• 电子书: EPUB/MOBI/AZW3\n" +
                      $"• 其他: APK/CRX/DMG/PYC/PAK/LNK/Java Class\n\n" +
                      $"功能:\n" +
                      $"• 十六进制/文本双视图\n" +
                      $"• 双引擎结构识别 (魔数匹配 + 启发式推断)\n" +
                      $"• 结构树 + 视图联动\n" +
                      $"• 手动编辑 + 撤销/重做\n" +
                      $"• 自定义格式规则 (JSON/YAML)\n" +
                      $"• 实时解码预览\n" +
                      $"• 多卷压缩包展开与懒加载\n" +
                      $"• EXE/DLL 加壳检测 (20+ 种)\n" +
                      $"• 异步字节搜索\n" +
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

        // 取消正在进行的识别
        _recognitionCts?.Cancel();
        _recognitionCts = null;

        // 清除分卷列表
        VolumeList.Clear();
        IsVolumeListVisible = false;
        _currentVolumeInfo = null;
        LivePreview.Clear();

        IsFileLoaded = false;
        SelectedTabIndex = 0;
        FileMeta.Clear();
        WindowTitle = "FileStruct - 二进制文件结构化分析工具";
        FileInfoText = "";
        StatusText = "文件已关闭";
        _logger.Info($"文件已关闭: {fileName}");
    }

    /// <summary>在识别结果中检测中央目录 → 标记当前分卷</summary>
    private void MarkVolumeHasCentralDir(StructureNode root)
    {
        if (_currentVolumeInfo == null) return;
        // 检测文件尾部是否有 EOCD 签名 (PK\x05\x06)
        bool hasCd = false;
        if (_buffer != null && _buffer.Length >= 22)
        {
            var tailSize = (int)Math.Min(_buffer.Length, 0x100FF);
            var tail = _buffer.ReadBytes(_buffer.Length - tailSize, tailSize);
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (tail[i] == 0x50 && tail[i + 1] == 0x4B &&
                    tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                { hasCd = true; break; }
            }
        }
        if (_currentVolumeIndex >= 0 && _currentVolumeIndex < VolumeList.Count)
            VolumeList[_currentVolumeIndex].HasCentralDir = hasCd;
    }

    /// <summary>立即更新纯文本 UI（不涉及耗时的 HexView/Tree）</summary>
    private void SyncTextUI(string volName)
    {
        if (_buffer == null) return;
        var ft = _fileService.DetectFileType(_buffer);
        WindowTitle = $"FileStruct - {volName}";
        FileInfoText = $"{Path.GetFileName(_buffer.FileName)} | {FormatFileSize(_buffer.Length)} | {ft.DisplayName}";
        FileMeta.Update(_buffer.FilePath, ft.DisplayName, _buffer.Length);
    }

    /// <summary>一致提交 HexView + 结构树（一次 Dispatcher 原子操作）</summary>
    private void CommitViews(StructureNode treeResult)
    {
        HexEditor.Buffer = _buffer;
        HexEditor.FileType = _fileService.DetectFileType(_buffer);
        StructureTree.LoadTree(treeResult);
        MarkVolumeHasCentralDir(treeResult);
    }

    /// <summary>切换到指定分卷文件（两步提交：先更新文字 UI，再后台加载+识别，最后原子提交视图）</summary>
    public async void SwitchToVolume(string volPath, int volIndex)
    {
        if (_currentVolumeInfo == null) return;
        try
        {
            // ── 去抖 ──
            _recognitionCts?.Cancel();
            _recognitionCts = new CancellationTokenSource();
            var ct = _recognitionCts.Token;

            // ── Step 1: 立即更新所有纯文本 UI ──
            _searchResults.Clear();
            _lastSearchedText = "";
            _searchResultIndex = 0;
            SearchResultText = "";
            _currentVolumeIndex = volIndex;

            for (int i = 0; i < VolumeList.Count; i++)
                VolumeList[i].IsCurrent = (i == volIndex);

            SelectedVolumeItem = VolumeList.FirstOrDefault(v => v.VolumeIndex == volIndex);

            var cached = VolumeList.FirstOrDefault(v => v.VolumeIndex == volIndex);

            // 加载文件到 _buffer（必须等 I/O 完成才能读类型）
            StatusText = $"正在加载分卷: {Path.GetFileName(volPath)}";
            ct.ThrowIfCancellationRequested();

            var newBuf = await _fileService.LoadFileAsync(volPath);
            _buffer?.Dispose();
            _buffer = newBuf;

            SyncTextUI(Path.GetFileName(volPath));
            StatusText = $"正在识别: {Path.GetFileName(volPath)}";

            // ── Step 2: 后台加载+识别 ──
            if (cached != null && cached.CachedRoot != null)
            {
                ct.ThrowIfCancellationRequested();
                StructureTree.LoadTree(cached.CachedRoot);
                CommitViews(cached.CachedRoot);
                StatusText = $"已切换到分卷（缓存）: {Path.GetFileName(volPath)}";
                return;
            }

            IsRecognizing = true;
            RecognitionProgress = 0;

            var bufRef = _buffer;
            var recognizer = _recognizer;

            var treeResult = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return recognizer.RecognizeAsync(bufRef,
                    new Progress<RecognitionProgress>(p =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusText = p.StatusText;
                            RecognitionProgress = p.Percentage;
                        });
                    }), ct).GetAwaiter().GetResult();
            }, ct);

            ct.ThrowIfCancellationRequested();

            // ── Step 3: 原子提交视图 ──
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CommitViews(treeResult);
            });

            if (cached != null)
            {
                cached.CachedRoot = StructureTree.RootNode;
                cached.IsLoaded = true;
            }

            StatusText = $"已切换到分卷: {Path.GetFileName(volPath)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "分卷切换已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"加载分卷失败: {ex.Message}";
        }
        finally
        {
            IsRecognizing = false;
            RecognitionProgress = 0;
        }
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
        if (_buffer == null) return;

        // 从菜单调用时输入框可能为空，弹窗提示输入
        var inputText = GotoOffsetText;
        if (string.IsNullOrWhiteSpace(inputText))
        {
            inputText = Microsoft.VisualBasic.Interaction.InputBox(
                "请输入偏移地址（十进制或0x十六进制）",
                "跳转到偏移", "0x");
            if (string.IsNullOrWhiteSpace(inputText)) return;
        }

        try
        {
            var offset = inputText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(inputText, 16)
                : long.Parse(inputText);

            if (offset < 0 || offset >= _buffer.Length)
            {
                StatusText = $"偏移越界: 0x{offset:X} (文件大小: 0x{_buffer.Length:X})";
                return;
            }

            HexEditor.NavigateToOffset = offset;
            HexEditor.NavigateToLength = 1;
            HexEditor.SelectionInfo = $"跳转到: 0x{offset:X}";
            StatusText = $"已跳转到偏移 0x{offset:X}";
            GotoOffsetText = "";
        }
        catch (FormatException)
        {
            StatusText = $"无效的偏移格式: {inputText} (支持十进制或 0x 十六进制)";
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

    private CancellationTokenSource? _searchCts;
    private List<long> _searchResults = [];
    private int _searchResultIndex = -1;
    private int _searchPatternLength;
    private string _lastSearchedText = "";

    [RelayCommand]
    private async Task SearchBytesAsync()
    {
        if (_buffer == null || string.IsNullOrWhiteSpace(SearchText)) return;

        // 搜索词未变且已有结果：等效于搜索下一个
        if (SearchText == _lastSearchedText && _searchResults.Count > 0)
        {
            NextSearchResult();
            return;
        }

        // 解析搜索模式
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

        _searchPatternLength = pattern.Length;
        _searchResults.Clear();
        _searchResultIndex = -1;

        // 取消前一次搜索
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        StatusText = "正在搜索...";
        var buffer = _buffer;
        var patternLen = pattern.Length;
        var patternCopy = (byte[])pattern.Clone();

        try
        {
            var results = await Task.Run(() =>
            {
                var localResults = new List<long>();
                for (long offset = 0; offset <= buffer.Length - patternLen; offset++)
                {
                    ct.ThrowIfCancellationRequested();

                    var match = true;
                    for (int i = 0; i < patternLen; i++)
                    {
                        if (buffer.ReadByte(offset + i) != patternCopy[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        localResults.Add(offset);
                        if (localResults.Count >= 1000) break;
                    }
                }
                return localResults;
            }, ct);

            _searchResults = results;
            _lastSearchedText = SearchText;

            if (_searchResults.Count == 0)
            {
                StatusText = "搜索完成: 未找到匹配";
                SearchResultText = "未找到匹配";
                return;
            }

            _searchResultIndex = 0;
            JumpToSearchResult();
        }
        catch (OperationCanceledException)
        {
            StatusText = "搜索已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败: {ex.Message}";
            _logger.Error("字节搜索失败", ex);
        }
    }

    [RelayCommand]
    private void NextSearchResult()
    {
        if (_searchResults.Count == 0) return;
        _searchResultIndex = (_searchResultIndex + 1) % _searchResults.Count;
        JumpToSearchResult();
    }

    [RelayCommand]
    private void PreviousSearchResult()
    {
        if (_searchResults.Count == 0) return;
        _searchResultIndex = (_searchResultIndex - 1 + _searchResults.Count) % _searchResults.Count;
        JumpToSearchResult();
    }

    private void JumpToSearchResult()
    {
        if (_searchResultIndex < 0 || _searchResultIndex >= _searchResults.Count) return;
        var offset = _searchResults[_searchResultIndex];
        var length = _searchPatternLength > 0 ? _searchPatternLength : 1;

        // 设置搜索导航守卫，防止树选择事件覆盖搜索高亮范围
        IsSearchNavigating = true;
        HexEditor.NavigateToOffset = offset;
        HexEditor.NavigateToLength = length;
        // 等待 WPF 事件循环完成后再清除守卫
        System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => IsSearchNavigating = false,
            System.Windows.Threading.DispatcherPriority.Background);

        SearchResultText = $"结果 {_searchResultIndex + 1}/{_searchResults.Count} @ 0x{offset:X}";
        StatusText = SearchResultText;
    }

    /// <summary>从选中的字节区间创建结构字段</summary>
    public void CreateFieldFromSelection(long offset, long length)
    {
        if (_buffer == null) return;
        var root = StructureTree.RootNode;
        if (root == null)
        {
            // 无识别结果时创建根节点
            root = new StructureNode
            {
                Name = "手动字段",
                Offset = offset,
                Length = length,
                DataType = FieldDataType.Bytes,
                Source = StructureNodeSource.UserCreated,
            };
            StructureTree.LoadTree(root);
            return;
        }
        var field = _editService.AddField(root, $"字段 @ 0x{offset:X}", offset, length);
        StructureTree.RefreshTree();
        _logger.Info($"创建字段: {field.Name} @ 0x{offset:X}, 长度 {length}");
        StatusText = $"已创建字段 @ 0x{offset:X}";
    }

    public IConfigService Config => _config;

    [RelayCommand]
    private void ShowSettings()
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        var win = new Views.SettingsWindow(vm);
        win.ShowDialog();
    }

    [RelayCommand]
    private async Task RecognizeAsync()
    {
        if (_buffer == null) return;
        if (HexEditor.FileType?.IsText == true)
        {
            StatusText = "纯文本文件无需结构识别";
            StructureTree.Clear();
            return;
        }
        await RunRecognitionAsync();
    }

    private async Task RunRecognitionAsync()
    {
        try
        {
            IsRecognizing = true;
            var result = await _recognizer.RecognizeAsync(_buffer!,
                new Progress<RecognitionProgress>(p =>
                {
                    StatusText = p.StatusText;
                    RecognitionProgress = p.Percentage;
                }));

            StructureTree.LoadTree(result);
            MarkVolumeHasCentralDir(result);

            StatusText = $"结构识别完成，共发现 {CountNodes(result)} 个字段";
            _logger.Info($"结构识别完成: {CountNodes(result)} 个节点");
        }
        catch (Exception ex)
        {
            var userAction = (ex as FileStruct.Core.Exceptions.FileStructException)?.UserAction;
            StatusText = $"识别失败: {ex.Message}";
            if (userAction != null) StatusText += $" — {userAction}";
            _logger.Error("结构识别失败", ex);
        }
        finally
        {
            IsRecognizing = false;
            RecognitionProgress = 0;
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
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
