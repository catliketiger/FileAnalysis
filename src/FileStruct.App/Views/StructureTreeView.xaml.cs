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
                // 先设偏移(不触发导航)，再设长度(触发导航，此时两个值都已就位)
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
                    _menuExpandZip.Visibility = IsArchiveNode(_contextNode.Node, mainVm.HexEditor.Buffer)
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
    private static bool IsArchiveNode(StructureNode node, BinaryBuffer buffer)
    {
        if (buffer == null || node.Offset < 0) return false;
        if (node.Offset + 4 > buffer.Length) return false;
        // ZIP: PK\x03\x04
        if (buffer.ReadUInt32(node.Offset, true) == 0x04034B50) return true;
        // RAR: Rar!\x1A\x07 (6 bytes)
        if (node.Offset + 6 <= buffer.Length && buffer.ReadUInt32(node.Offset, true) == 0x21726152 &&
            buffer.ReadUInt16(node.Offset + 4, true) == 0x071A) return true;
        return false;
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
        if (!IsArchiveNode(item.Node, buffer)) { mainVm.StatusText = "该节点不是压缩包格式"; return; }

        try
        {
            // 检测归档类型
            var sig32 = buffer.ReadUInt32(item.Node.Offset, true);
            if (sig32 == 0x04034B50)
            {
                ExpandZipArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0x21726152) // "Rar!"
            {
                var rarVer = buffer.ReadByte(item.Node.Offset + 6);
                System.Diagnostics.Debug.WriteLine($"[RAR] 版本字节={rarVer}, 文件大小={buffer.Length}");
                if (rarVer == 1)
                {
                    var count = ExpandRar5Archive(item, buffer, mainVm);
                    if (count > 0) { mainVm.StatusText = $"RAR5展开完成，共{count}个条目"; }
                    else
                    {
                        mainVm.StatusText = "RAR5展开未识别到条目，尝试RAR4模式...";
                        ExpandRar4Archive(item, buffer, mainVm);
                    }
                }
                else
                {
                    mainVm.StatusText = "正在解析RAR4格式...";
                    ExpandRar4Archive(item, buffer, mainVm);
                }
            }
        }
        catch (Exception ex)
        {
            mainVm.StatusText = $"展开失败: {ex.Message}";
        }
    }

    private void ExpandZipArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
    {
        // 1. 查找 EOCD
        var eocdOff = FindEocd(buffer);
        if (eocdOff < 0) { mainVm.StatusText = "无法定位 ZIP 目录"; return; }

        // 2. 解析 EOCD
        var totalEntries = buffer.ReadUInt16(eocdOff + 8, true);
        var cdOffset = buffer.ReadUInt32(eocdOff + 16, true);
        if (totalEntries == 0) { mainVm.StatusText = "压缩包为空"; return; }

            // 3. 解析中央目录条目 → 收集 (localHeaderOffset, compSize, uncompSize, method, filename)
            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();
            long cdPos = cdOffset;
            int count = 0;
            bool hasEncrypted = false;

            while (count < totalEntries && count < 10000 && cdPos + 46 <= buffer.Length)
            {
                var sig = buffer.ReadUInt32(cdPos, true);
                if (sig != 0x02014B50) break;

                var flags = buffer.ReadUInt16(cdPos + 8, true);
                var isEncrypted = (flags & 1) != 0;

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
                var desc = isEncrypted ? $"[加密] {methodStr}: {compSize} → {uncompSize} bytes" : $"{methodStr}: {compSize} → {uncompSize} bytes";

                entries.Add((dataOffset, dataLen, displayName, desc, fileName, isEncrypted));
                cdPos += 46 + nameLen + extraLen + commentLen;
                count++;
            }

            if (hasEncrypted)
            {
                var pwd = Microsoft.VisualBasic.Interaction.InputBox(
                    "该压缩包包含加密文件，请输入密码：", "压缩包密码", "");
                if (!string.IsNullOrEmpty(pwd))
                    mainVm.StatusText = $"已输入密码，共展示条目（暂不支持导出加密文件）";
            }

            BuildArchiveTree(item.Node, entries, mainVm);
        }

        // ===== RAR5 解析 =====
        // RAR5 使用可变长整数(vint)编码, 每字节低7位为数据, MSB为继续标志
        private static (ulong value, int bytesRead) ReadRar5Vint(byte[] data, int offset)
        {
            ulong result = 0;
            int shift = 0;
            int i = 0;
            while (i < 9 && offset + i < data.Length)
            {
                var b = data[offset + i];
                result |= (ulong)(b & 0x7F) << shift;
                shift += 7;
                i++;
                if ((b & 0x80) == 0) break;
            }
            return (result, i);
        }

        private int ExpandRar5Archive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            System.Diagnostics.Debug.WriteLine("[RAR5] 开始展开");
            var startOffset = item.Node.Offset;
            long pos = startOffset + 8; // 跳过 "Rar!\x1A\x07\x01\x00" 标记(8字节)
            System.Diagnostics.Debug.WriteLine($"[RAR5] startOffset={startOffset}, pos={pos}, fileLen={buffer.Length}");
            int count = 0;
            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();

            while (pos + 8 <= buffer.Length && count < 10000)
            {
                // CRC32 (4 bytes, we just skip it)
                // HeaderSize (vint) → header 总大小
                var hdrBytes = ReadRar5Block(buffer, pos, out int hdrSize);
                if (hdrBytes == null || hdrSize < 2) { System.Diagnostics.Debug.WriteLine($"[RAR5] 块空或太小 pos={pos}"); break; }

                System.Diagnostics.Debug.WriteLine($"[RAR5] 块 pos={pos}, hdrSize={hdrSize}, 前4字节={BitConverter.ToString(hdrBytes, 0, Math.Min(4, hdrBytes.Length))}");

                int off = 4; // skip CRC32
                var (hdrSizeVal, hdrSizeLen) = ReadRar5Vint(hdrBytes, off);
                off += hdrSizeLen;
                if (hdrSizeVal < 2UL || hdrSizeVal > (ulong)hdrBytes.Length) { System.Diagnostics.Debug.WriteLine($"[RAR5] hdrSizeVal={hdrSizeVal} 异常"); break; }

                var (hdrType, typeLen) = ReadRar5Vint(hdrBytes, off);
                off += typeLen;
                System.Diagnostics.Debug.WriteLine($"[RAR5] hdrType={hdrType}");

                var (hdrFlags, flagsLen) = ReadRar5Vint(hdrBytes, off);
                off += flagsLen;
                var hdrFlagsInt = (int)hdrFlags;

                ulong dataAreaSize = 0;
                if ((hdrFlagsInt & 0x0001) != 0) // ExtraAreaSize
                {
                    var (extraSize, extraLen) = ReadRar5Vint(hdrBytes, off);
                    off += extraLen;
                    off += (int)extraSize;
                }
                if ((hdrFlagsInt & 0x0002) != 0) // DataAreaSize
                {
                    var (daSize, daLen) = ReadRar5Vint(hdrBytes, off);
                    off += daLen;
                    dataAreaSize = daSize;
                }

                bool isEnc = (hdrFlagsInt & 0x0040) != 0;
                var hdrTypeInt = (int)hdrType;

                if (hdrTypeInt == 2) // 文件头
                {
                    var (compInfo, compLen) = ReadRar5Vint(hdrBytes, off);
                    off += compLen;
                    bool isDir = (compInfo & (1UL << 11)) != 0;

                    ulong unpackedSize = 0;
                    if ((compInfo & (1UL << 12)) != 0 && off + 8 <= hdrBytes.Length)
                    {
                        unpackedSize = BitConverter.ToUInt64(hdrBytes, off);
                        off += 8;
                    }
                    else
                    {
                        var (us, usLen) = ReadRar5Vint(hdrBytes, off);
                        off += usLen;
                        unpackedSize = us;
                    }

                    if ((compInfo & (1UL << 13)) != 0) // salt present, skip 8 bytes
                    {
                        if (off + 8 <= hdrBytes.Length) off += 8;
                    }

                    var (attr, attrLen) = ReadRar5Vint(hdrBytes, off);
                    off += attrLen;
                    var (ftime, ftimeLen) = ReadRar5Vint(hdrBytes, off);
                    off += ftimeLen;

                    string fileName;
                    if (off < hdrBytes.Length)
                    {
                        fileName = System.Text.Encoding.UTF8.GetString(hdrBytes, off, hdrBytes.Length - off);
                        var nullIdx = fileName.IndexOf('\0');
                        if (nullIdx >= 0) fileName = fileName[..nullIdx];
                    }
                    else fileName = $"(file_{count})";

                    if (!isDir)
                    {
                        long dataOff = pos + hdrSize;
                        long dataLen = (long)dataAreaSize;
                        var methodStr = $"v{compInfo & 0x3FF}";
                        var displayName = $"{fileName}  [{methodStr}]  {FormatSize((long)unpackedSize)}";
                        var desc = isEnc ? $"[加密] RAR5 {methodStr}: {unpackedSize} bytes" : $"RAR5 {methodStr}: {unpackedSize} bytes";
                        entries.Add((dataOff, dataLen > 0 ? dataLen : (long)unpackedSize,
                            displayName, desc, fileName, isEnc));
                        count++;
                        System.Diagnostics.Debug.WriteLine($"[RAR5] 发现文件: {fileName}, size={unpackedSize}, off={dataOff}");
                    }
                }
                else if (hdrTypeInt == 5) // 结束块
                { System.Diagnostics.Debug.WriteLine("[RAR5] 结束块"); break; }

                pos += hdrSize + (long)dataAreaSize;
                if (pos >= buffer.Length) break;
            }
            System.Diagnostics.Debug.WriteLine($"[RAR5] 展开结束，共 {count} 个文件");

            if (count > 0)
            {
                BuildArchiveTree(item.Node, entries, mainVm);
                return count;
            }

            // 后备方案：扫描文件名模式
            System.Diagnostics.Debug.WriteLine("[RAR5] 尝试文件名扫描模式");
            mainVm.StatusText = "正在扫描RAR5文件名...";

            long scanStart = startOffset + 8;
            long scanEnd = Math.Min(startOffset + buffer.Length, scanStart + 1024);
            var textBuf = new byte[scanEnd - scanStart];
            for (int i = 0; i < textBuf.Length; i++)
                textBuf[i] = buffer.ReadByte(scanStart + i);

            // 在数据中搜索可打印文件名(包含.或/的字符串)
            for (int i = 0; i < textBuf.Length; i++)
            {
                if (textBuf[i] >= 0x20 && textBuf[i] < 0x7F)
                {
                    int j = i;
                    while (j < textBuf.Length && textBuf[j] >= 0x20 && textBuf[j] < 0x7F) j++;
                    if (j - i >= 3 && j - i <= 200)
                    {
                        var name = System.Text.Encoding.ASCII.GetString(textBuf, i, j - i);
                        if ((name.Contains('.') || name.Contains('/') || name.Contains('\\')) &&
                            !entries.Any(e => e.path == name))
                        {
                            var displayName = $"{name}  [v?]  {FormatSize(0)}";
                            entries.Add((0, 0, displayName, "RAR5 条目", name, false));
                            count++;
                            if (count >= 100) break;
                        }
                    }
                    i = j;
                }
            }

            if (count > 0)
                BuildArchiveTree(item.Node, entries, mainVm);
            else
                mainVm.StatusText = "RAR5展开未识别到文件条目";
            return count;
        }

        /// <summary>读取 RAR5 块的完整数据（CRC + header + data）</summary>
        private static byte[]? ReadRar5Block(BinaryBuffer buffer, long pos, out int hdrSize)
        {
            hdrSize = 0;
            if (pos + 7 > buffer.Length) return null;
            // 先读前 8 字节：CRC(4) + headerSize的vint编码
            var head = buffer.ReadBytes(pos, Math.Min(8, (int)(buffer.Length - pos)));
            if (head.Length < 5) return null;
            var (hdrSizeVal, hdrSizeLen) = ReadRar5Vint(head, 4);
            if (hdrSizeVal == 0 || hdrSizeVal > 1024 * 1024) return null; // 限制1MB
            hdrSize = 4 + hdrSizeLen + (int)hdrSizeVal;
            if (pos + hdrSize > buffer.Length) return null;
            return buffer.ReadBytes(pos, hdrSize);
        }

        private void ExpandRar4Archive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            long pos = 7; // 跳过 "Rar!\x1A\x07\x00" (7字节)
            int count = 0;
            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();

            // RAR4 每个块头都有 HEAD_SIZE 字段在偏移 5-6
            while (pos + 7 <= buffer.Length && count < 10000)
            {
                var headerType = buffer.ReadByte(pos + 2);
                var headerFlags = buffer.ReadUInt16(pos + 3, true);
                var headSize = buffer.ReadUInt16(pos + 5, true); // HEAD_SIZE 总头大小
                var isEnc = (headerFlags & 0x0004) != 0;
                var hasLarge = (headerFlags & 0x0100) != 0; // LARGE_FILE

                if (headerType == 0x74) // 文件头 (HEAD_FILE)
                {
                    // +0: CRC(2) +2:Type(1) +3:Flags(2) +5:HEAD_SIZE(2) = 7字节基头
                    // +7: HIGH_PACK_SIZE(4) +11: HIGH_UNP_SIZE(4) [if LARGE_FILE]
                    // +7 或 +15: PACK_SIZE(4) / UNP_SIZE(4)
                    int sOff = 7;
                    if (hasLarge) sOff += 8;

                    var compSize = (long)buffer.ReadUInt32(pos + sOff, true);
                    var uncompSize = (long)buffer.ReadUInt32(pos + sOff + 4, true);
                    if (hasLarge)
                    {
                        compSize |= (long)buffer.ReadUInt32(pos + 7, true) << 32;
                        uncompSize |= (long)buffer.ReadUInt32(pos + 11, true) << 32;
                        sOff = 15; // 固定字段起始偏移(有LARGE)
                    }
                    else sOff = 7; // 固定字段起始偏移(无LARGE)
                    // 后续字段: OS(1) CRC(4) TIME(4) VER(1) METHOD(1) NAMESIZE(2) ATTR(4) = 17字节

                    var os = buffer.ReadByte(pos + sOff + 8);
                    var fileCRC = buffer.ReadUInt32(pos + sOff + 9, true);
                    var fileTime = buffer.ReadUInt32(pos + sOff + 13, true);
                    var ver = buffer.ReadByte(pos + sOff + 17);
                    var method = buffer.ReadByte(pos + sOff + 18);
                    int nameSize = buffer.ReadUInt16(pos + sOff + 19, true);
                    // attr at sOff + 21 (4 bytes)

                    string fileName;
                    int nameOff = sOff + 21 + 4; // attr(4)后才是文件名
                    if (nameSize > 0 && pos + nameOff + nameSize <= buffer.Length)
                    {
                        var nameBytes = buffer.ReadBytes(pos + nameOff, nameSize);
                        fileName = System.Text.Encoding.UTF8.GetString(nameBytes);
                    }
                    else fileName = $"(file_{count})";

                    var methodStr = method <= 5 ? $"v{method}" : $"?{method}";
                    var displayName = $"{fileName}  [{methodStr}]  {FormatSize(uncompSize)}";
                    var dataOff = pos + headSize;
                    var rarDesc = isEnc ? $"[加密] RAR4 {methodStr}: {compSize} → {uncompSize} bytes" : $"RAR4 {methodStr}: {compSize} → {uncompSize} bytes";
                    entries.Add((dataOff, compSize > 0 ? compSize : Math.Max(1, uncompSize),
                        displayName, rarDesc, fileName, isEnc));
                    count++;

                    pos = dataOff + (compSize > 0 ? compSize : 0);
                }
                else if (headerType == 0x73) // 归档头
                    pos += headSize;
                else if (headerType == 0x7B) // 结束块
                    break;
                else
                    pos += headSize;

                if (pos >= buffer.Length || pos < 0) break;
            }

            if (count > 0)
                BuildArchiveTree(item.Node, entries, mainVm);
            else
                mainVm.StatusText = "未找到RAR4压缩条目";
        }

        private static void BuildArchiveTree(StructureNode parent,
            List<(long dataOffset, long length, string displayName, string description, string filePath, bool isEncrypted)> entries,
            MainViewModel mainVm)
        {
            if (entries.Count == 0) { mainVm.StatusText = "未找到有效条目"; return; }

            var dirNodes = new Dictionary<string, StructureNode> { ["."] = parent };

            foreach (var (dataOff, dataLen, dispName, desc, filePath, isEnc) in entries)
            {
                var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var dirPath = ".";
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var childPath = dirPath + "/" + parts[i];
                    if (!dirNodes.ContainsKey(childPath))
                    {
                        var dirNode = new StructureNode
                        {
                            Name = parts[i] + "/",
                            Offset = 0, Length = 0,
                            DataType = FieldDataType.Struct,
                            Confidence = 1.0,
                            Source = StructureNodeSource.UserCreated,
                        };
                        dirNodes[dirPath].AddChild(dirNode);
                        dirNodes[childPath] = dirNode;
                    }
                    dirPath = childPath;
                }

                dirNodes[dirPath].AddChild(new StructureNode
                {
                    Name = dispName, Offset = dataOff, Length = dataLen,
                    DataType = FieldDataType.Bytes, Confidence = 1.0,
                    Source = StructureNodeSource.UserCreated, Description = desc,
                    IsEncrypted = isEnc,
                });
            }

            mainVm.StructureTree.RefreshTree();
            mainVm.StatusText = $"压缩包展开完成，共 {entries.Count} 个条目";
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
