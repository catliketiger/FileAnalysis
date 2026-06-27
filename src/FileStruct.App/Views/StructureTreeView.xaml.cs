using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileStruct.App.ViewModels;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.App.Views;

public partial class StructureTreeView : UserControl
{
    private bool _isSyncingSelection;

    public StructureTreeView()
    {
        InitializeComponent();
        StructTree.SelectedItemChanged += OnSelectedItemChanged;
        BuildContextMenu();
    }

    private ContextMenu? _treeContextMenu;
    private MenuItem? _menuExpandZip;

    private void BuildContextMenu()
    {
        _treeContextMenu = new ContextMenu();
        _treeContextMenu.Items.Add(new MenuItem { Header = "添加子字段" });
        _treeContextMenu.Items.Add(new MenuItem { Header = "删除字段" });
        _treeContextMenu.Items.Add(new MenuItem { Header = "编辑字段…" });
        _treeContextMenu.Items.Add(new Separator());
        _menuExpandZip = new MenuItem { Header = "展开压缩包" };
        _treeContextMenu.Items.Add(_menuExpandZip);
        _treeContextMenu.Items.Add(new Separator());
        _treeContextMenu.Items.Add(new MenuItem { Header = "导出结构…" });
        _treeContextMenu.Items.Add(new MenuItem { Header = "导入结构…" });
        foreach (var item in _treeContextMenu.Items.OfType<MenuItem>())
            item.Click += OnContextMenuItemClick;

        // 挂到 TreeView 上保证右键能触发
        StructTree.ContextMenu = _treeContextMenu;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // 重入守卫：防止 Search→选中→Hex导航→Hex选择变化→SelectNodeByOffset→重入SelectedItemChanged
        if (_isSyncingSelection) return;

        if (e.NewValue is TreeItemViewModel item &&
            DataContext is MainViewModel mainVm)
        {
            _isSyncingSelection = true;
            // 内部导航标识：防止 HexView 回馈时 SelectNodeByOffset 覆盖树选中
            using var guard = mainVm.StructureTree.BeginInternalNavigation();
            try
            {
                var node = item.Node;
                mainVm.StructureTree.SelectedNode = node;
                // 使用 NavigateToOffset 实现居中 + 高亮
                mainVm.HexEditor.NavigateToOffset = node.Offset;
                mainVm.HexEditor.NavigateToLength = (int)Math.Max(1, node.Length);
                mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
                mainVm.StatusText = $"已定位到字段: {node.Name}";
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }

    // ===== 右键菜单 =====

    private TreeItemViewModel? _contextNode;

    private void StructTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe)
        {
            var tvi = fe as TreeViewItem ?? FindParent<TreeViewItem>(fe);
            _contextNode = tvi?.DataContext as TreeItemViewModel;
            if (_contextNode != null)
            {
                // 仅对 ZIP 节点显示「展开压缩包」
                if (_menuExpandZip != null && DataContext is MainViewModel mainVm)
                {
                    _menuExpandZip.Visibility = IsZipNode(_contextNode.Node, mainVm.HexEditor.Buffer)
                        ? Visibility.Visible : Visibility.Collapsed;
                }
                return;
            }
        }
        _contextNode = null;
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        while (child != null)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent is T result) return result;
            child = parent;
        }
        return null;
    }

    private void OnContextMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || _contextNode == null || DataContext is not MainViewModel mainVm) return;
        var item = _contextNode;

        switch (mi.Header as string)
        {
            case "添加子字段":
                AddChildField(item, mainVm);
                break;
            case "删除字段":
                DeleteField(item, mainVm);
                break;
            case "编辑字段…":
                EditField(item, mainVm);
                break;
            case "展开压缩包":
                ExpandZip(item, mainVm);
                break;
            case "导出结构…":
                ExportStructure(item, mainVm);
                break;
            case "导入结构…":
                ImportStructure(item, mainVm);
                break;
        }
    }

    private void AddChildField(TreeItemViewModel item, MainViewModel mainVm)
    {
        bool retry;
        do
        {
            retry = false;
            var vm = FieldEditViewModel.ForNew(item.Node);
            var dialog = new FieldEditDialog(vm);
            if (dialog.ShowDialog() != true) return;

            var newOff = vm.ParsedOffset;
            var newEnd = newOff + vm.FieldLength;
            var parentEnd = item.Node.Offset + item.Node.Length;
            if (newOff < item.Node.Offset || newEnd > parentEnd)
            {
                MessageBox.Show($"子字段范围 (0x{newOff:X} - 0x{newEnd:X}) 超出父节点范围 (0x{item.Node.Offset:X} - 0x{parentEnd:X})",
                    "范围错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                retry = true;
                continue;
            }
            var node = new StructureNode
            {
                Name = vm.FieldName,
                Offset = newOff,
                Length = vm.FieldLength,
                DataType = vm.DataType,
                Endianness = vm.Endianness,
                Confidence = 1.0,
                Source = StructureNodeSource.UserCreated,
            };
            if (!mainVm.StructureTree.AddChildNode(item.Node, node))
            {
                MessageBox.Show("嵌套深度不能超过 15 层", "限制", MessageBoxButton.OK, MessageBoxImage.Warning);
                retry = true;
                continue;
            }
            mainVm.StatusText = $"已添加字段: {node.Name} @ 0x{node.Offset:X}";
        } while (retry);
    }

    private void DeleteField(TreeItemViewModel item, MainViewModel mainVm)
    {
        if (item.Node.Parent == null) return; // 根节点不可删

        var result = MessageBox.Show($"确认删除字段 \"{item.Node.Name}\" 及其子节点？",
            "删除字段", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        mainVm.StructureTree.DeleteNode(item.Node);
        mainVm.StatusText = $"已删除字段: {item.Node.Name}";
    }

    private void EditField(TreeItemViewModel item, MainViewModel mainVm)
    {
        bool retry;
        do
        {
            retry = false;
            var vm = FieldEditViewModel.ForEdit(item.Node);
            var dialog = new FieldEditDialog(vm);
            if (dialog.ShowDialog() != true) return;

            var newOff = vm.ParsedOffset;
            var newLen = vm.FieldLength;
            var newEnd = newOff + newLen;

            // 校验范围不超父节点
            if (item.Node.Parent != null)
            {
                var parentEnd = item.Node.Parent.Offset + item.Node.Parent.Length;
                if (newOff < item.Node.Parent.Offset || newEnd > parentEnd)
                {
                    MessageBox.Show($"字段范围 (0x{newOff:X} - 0x{newEnd:X}) 超出父节点范围 (0x{item.Node.Parent.Offset:X} - 0x{parentEnd:X})",
                        "范围错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    retry = true;
                    continue;
                }
            }

            item.Node.Name = vm.FieldName;
            if (newOff != item.Node.Offset)
            {
                item.Node.Offset = newOff;
                item.Node.Source = StructureNodeSource.UserModified;
            }
            if (newLen != item.Node.Length)
            {
                item.Node.Length = newLen;
                item.Node.Source = StructureNodeSource.UserModified;
            }
            if (vm.DataType != item.Node.DataType)
            {
                item.Node.DataType = vm.DataType;
                item.Node.Source = StructureNodeSource.UserModified;
            }
            item.Node.Endianness = vm.Endianness;
            mainVm.StatusText = $"已更新字段: {item.Node.Name}";
        } while (retry);
    }

    private void ExportStructure(TreeItemViewModel item, MainViewModel mainVm)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出结构定义",
            DefaultExt = ".json",
            Filter = "JSON 规则文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            // 如果第一个子字段在偏移0且有数据，自动从文件读取魔数
            byte[]? magicBytes = null;
            int magicOffset = 0;
            var firstChild = item.Node.Children.FirstOrDefault();
            if (firstChild != null && firstChild.Offset == 0 && firstChild.Length > 0)
            {
                // 尝试从打开的Buffer读取魔数字节
                if (mainVm.HexEditor.Buffer != null)
                {
                    var len = (int)Math.Min(firstChild.Length, 32);
                    magicBytes = mainVm.HexEditor.Buffer.ReadBytes(0, len);
                }
            }

            var json = StructureTreeViewModel.ExportAsJson(item.Node, item.Node.Name,
                magicBytes, magicOffset);
            File.WriteAllText(dialog.FileName, json);
            mainVm.StatusText = $"结构已导出: {dialog.FileName}" +
                (magicBytes != null ? $" (包含 {magicBytes.Length} 字节魔数)" : "");
        }
        catch (Exception ex)
        {
            mainVm.StatusText = $"导出失败: {ex.Message}";
        }
    }

    // ===== ZIP 展开 =====

    /// <summary>判断节点数据是否以 ZIP 魔数开头</summary>
    private static bool IsZipNode(StructureNode node, BinaryBuffer buffer)
    {
        if (buffer == null || node.Offset < 0 || node.Offset + 4 > buffer.Length) return false;
        return buffer.ReadUInt32(node.Offset, true) == 0x04034B50;
    }

    /// <summary>扫描末尾查找 EOCD 签名 (0x06054B50)</summary>
    private static long FindEocd(BinaryBuffer buffer)
    {
        var tailSize = (int)Math.Min(buffer.Length, 0x100FF);
        var tail = buffer.ReadBytes(buffer.Length - tailSize, tailSize);
        // 从后向前查找
        for (int i = tail.Length - 22; i >= 0; i--)
            if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                return buffer.Length - tailSize + i;
        return -1;
    }

    private void ExpandZip(TreeItemViewModel item, MainViewModel mainVm)
    {
        var buffer = mainVm.HexEditor.Buffer;
        if (buffer == null) { mainVm.StatusText = "没有已打开的文件"; return; }
        if (!IsZipNode(item.Node, buffer)) { mainVm.StatusText = "该节点不是压缩包格式"; return; }

        try
        {
            // 1. 查找 EOCD
            var eocdOff = FindEocd(buffer);
            if (eocdOff < 0) { mainVm.StatusText = "无法定位 ZIP 目录"; return; }

            // 2. 解析 EOCD
            var totalEntries = buffer.ReadUInt16(eocdOff + 8, true);
            var cdOffset = buffer.ReadUInt32(eocdOff + 16, true);
            if (totalEntries == 0) { mainVm.StatusText = "压缩包为空"; return; }

            // 3. 解析中央目录条目 → 收集 (localHeaderOffset, compSize, uncompSize, method, filename)
            var entries = new List<(long dataOffset, long length, string displayName, string description, string path)>();
            long cdPos = cdOffset;
            int count = 0;

            while (count < totalEntries && count < 10000 && cdPos + 46 <= buffer.Length)
            {
                var sig = buffer.ReadUInt32(cdPos, true);
                if (sig != 0x02014B50) break;

                var compMethod = buffer.ReadUInt16(cdPos + 10, true);
                var compSize = buffer.ReadUInt32(cdPos + 20, true);
                var uncompSize = buffer.ReadUInt32(cdPos + 24, true);
                var nameLen = buffer.ReadUInt16(cdPos + 28, true);
                var extraLen = buffer.ReadUInt16(cdPos + 30, true);
                var commentLen = buffer.ReadUInt16(cdPos + 32, true);
                var localHeaderOff = buffer.ReadUInt32(cdPos + 42, true);

                // 读文件名
                string fileName;
                if (nameLen > 0)
                {
                    var nameBytes = buffer.ReadBytes(cdPos + 46, nameLen);
                    fileName = System.Text.Encoding.UTF8.GetString(nameBytes);
                }
                else
                {
                    fileName = $"(entry_{count})";
                }

                // 从本地文件头读 extraLen 以计算数据偏移
                long dataOffset = 0;
                var methodStr = compMethod switch { 0 => "Stored", 8 => "Deflate", 12 => "BZIP2", _ => $"M{compMethod}" };

                if (localHeaderOff + 30 <= buffer.Length)
                {
                    var localExtraLen = buffer.ReadUInt16(localHeaderOff + 28, true);
                    dataOffset = localHeaderOff + 30 + nameLen + localExtraLen;
                }

                var dataLen = compSize > 0 ? (long)compSize : (long)uncompSize;
                var displayName = $"{fileName}  [{methodStr}]  {FormatSize(uncompSize)}";
                var desc = $"{methodStr}: {compSize} → {uncompSize} bytes";

                entries.Add((dataOffset, dataLen, displayName, desc, fileName));
                cdPos += 46 + nameLen + extraLen + commentLen;
                count++;
            }

            if (entries.Count == 0) { mainVm.StatusText = "未找到有效条目"; return; }

            // 4. 构建目录树：按 '/' 拆分路径，创建层级结构
            var parent = item.Node;
            var dirNodes = new Dictionary<string, StructureNode> { ["."] = parent };

            foreach (var (dataOff, dataLen, dispName, desc, filePath) in entries)
            {
                // 按 '/' 拆分路径
                var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                // 确保目录路径存在
                var dirPath = ".";
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var childPath = dirPath + "/" + parts[i];
                    if (!dirNodes.ContainsKey(childPath))
                    {
                        var dirNode = new StructureNode
                        {
                            Name = parts[i] + "/",
                            Offset = 0,
                            Length = 0,
                            DataType = FieldDataType.Struct,
                            Confidence = 1.0,
                            Source = StructureNodeSource.AutoDetected,
                        };
                        dirNodes[dirPath].AddChild(dirNode);
                        dirNodes[childPath] = dirNode;
                    }
                    dirPath = childPath;
                }

                // 添加文件节点
                var fileNode = new StructureNode
                {
                    Name = dispName,
                    Offset = dataOff,
                    Length = dataLen,
                    DataType = FieldDataType.Bytes,
                    Confidence = 1.0,
                    Source = StructureNodeSource.AutoDetected,
                    Description = desc,
                };
                dirNodes[dirPath].AddChild(fileNode);
            }

            mainVm.StructureTree.RefreshTree();
            mainVm.StatusText = $"压缩包展开完成，共 {count} 个条目";
        }
        catch (Exception ex)
        {
            mainVm.StatusText = $"展开失败: {ex.Message}";
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1}KB";
        return $"{bytes / (1024.0 * 1024.0):F1}MB";
    }

    private void ImportStructure(TreeItemViewModel item, MainViewModel mainVm)
    {
        var openDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入结构定义",
            DefaultExt = ".json",
            Filter = "JSON 规则文件 (*.json)|*.json"
        };
        if (openDialog.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(openDialog.FileName);
            var imported = StructureTreeViewModel.ImportFromJson(json);
            // 清空原树，用导入的结构替换
            mainVm.StructureTree.Clear();
            mainVm.StructureTree.LoadTree(imported);
            mainVm.StructureTree.RootNode.Source = StructureNodeSource.UserCreated;
            mainVm.StatusText = $"已从 {openDialog.FileName} 导入结构";
        }
        catch (Exception ex)
        {
            mainVm.StatusText = $"导入失败: {ex.Message}";
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel mainVm)
        {
            var text = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            var found = mainVm.StructureTree.SearchTree(text);
            if (found)
            {
                var matchIndex = mainVm.StructureTree.GetLastMatchIndex();
                var totalMatches = mainVm.StructureTree.GetTotalMatches();
                var matchText = totalMatches > 1
                    ? $"匹配 {matchIndex + 1}/{totalMatches}"
                    : "";
                mainVm.StatusText = $"已定位到字段: {text}  {matchText}".Trim();

                // 异步 BringIntoView：等待 TreeView 容器生成后再滚动
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        if (StructTree.SelectedItem is TreeItemViewModel selItem)
                        {
                            if (StructTree.ItemContainerGenerator
                                .ContainerFromItem(selItem) is TreeViewItem tvi)
                            {
                                tvi.BringIntoView();
                            }
                        }
                    }));
            }
            else
            {
                mainVm.StatusText = $"未找到匹配的字段: {text}";
            }
        }
    }
}
