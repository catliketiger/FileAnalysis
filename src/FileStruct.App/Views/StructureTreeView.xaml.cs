using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileStruct.App.ViewModels;
using FileStruct.App.Utils;
using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;
using FileStruct.Services.StructureRecognition.SevenZipParser;

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
            // 搜索导航守卫：搜索时不要用树节点的范围覆盖搜索高亮
            if (mainVm.IsSearchNavigating) return;

            _isSyncingSelection = true;
            // 内部导航标识：防止 HexView 回馈时 SelectNodeByOffset 覆盖树选中
            using var guard = mainVm.StructureTree.BeginInternalNavigation();
            try
            {
                var node = item.Node;
                mainVm.StructureTree.SelectedNode = node;
                mainVm.HexEditor.NavigateToOffset = node.Offset;
                mainVm.HexEditor.NavigateToLength = (int)Math.Max(1, node.Length);
                mainVm.HexEditor.SelectionInfo = $"字段: {node.Name} @ 0x{node.Offset:X}, 长度 {node.Length}";
                mainVm.StatusText = $"已定位到字段: {node.Name}";
                // 强制 WPF 立即处理所有待处理的 DataBinding 更新，确保两个 DP 都已设置
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    () => { }, System.Windows.Threading.DispatcherPriority.Background);
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
                // 对 ZIP/EPUB 节点显示「展开内容」
                if (_menuExpandZip != null && DataContext is MainViewModel mainVm)
                {
                    var buffer = mainVm.HexEditor.Buffer;
                    if (buffer != null && IsArchiveNode(_contextNode.Node, buffer))
                    {
                        _menuExpandZip.Visibility = Visibility.Visible;
                        // 根据文件类型定制菜单文字
                        var nodeName = _contextNode.Node.Parent?.Name ?? "";
                        var isEpub = nodeName.Contains("EPUB", StringComparison.OrdinalIgnoreCase);
                        var isCrx = nodeName.Contains("CRX", StringComparison.OrdinalIgnoreCase);
                        var isPak = nodeName.Contains("PAK", StringComparison.OrdinalIgnoreCase);
                        _menuExpandZip.Header = isEpub ? "展开电子书内容"
                            : isCrx ? "展开扩展包"
                            : isPak ? "展开资源包"
                            : "展开压缩包";
                    }
                    else
                    {
                        _menuExpandZip.Visibility = Visibility.Collapsed;
                    }
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

    /// <summary>判断节点数据是否为可展开的压缩包格式</summary>
    private static bool IsArchiveNode(StructureNode node, BinaryBuffer buffer)
    {
        if (buffer == null || node.Offset < 0) return false;
        // 跳过虚拟节点（如 BuildArchiveTree 创建的目录节点，offset=0, length=0）
        if (node.Length == 0 && node.DataType == FieldDataType.Struct) return false;
        if (node.Offset + 4 > buffer.Length) return false;
        // ZIP: PK\x03\x04 (LH) / PK\x01\x02 (CD) / PK\x05\x06 (EOCD) / 分卷签名
        var sig = buffer.ReadUInt32(node.Offset, true);
        if (sig == 0x04034B50 || sig == 0x02014B50 || sig == 0x06054B50 ||
            sig == 0x08074B50 || sig == 0x30304B50) return true;
        // CRX: Cr24 (Chrome 扩展)
        if (buffer.ReadUInt32(node.Offset, true) == 0x34327243) return true;
        // PAK: PACK (Unreal 资源包)
        if (buffer.ReadUInt32(node.Offset, true) == 0x4B434150) return true;
        // CAB: MSCF (Microsoft Cabinet)
        if (buffer.ReadUInt32(node.Offset, true) == 0x4643534D) return true;
        // 7z: "7z\xBC\xAF" header
        if (node.Offset + 6 <= buffer.Length &&
            buffer.ReadByte(node.Offset) == 0x37 && buffer.ReadByte(node.Offset + 1) == 0x7A &&
            buffer.ReadByte(node.Offset + 2) == 0xBC && buffer.ReadByte(node.Offset + 3) == 0xAF) return true;
        // GZip: 0x1F 0x8B 0x08
        if (node.Offset + 3 <= buffer.Length &&
            buffer.ReadByte(node.Offset) == 0x1F && buffer.ReadByte(node.Offset + 1) == 0x8B &&
            buffer.ReadByte(node.Offset + 2) == 0x08) return true;
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
            if (sig32 == 0x04034B50 || sig32 == 0x02014B50 || sig32 == 0x06054B50 ||
                sig32 == 0x08074B50 || sig32 == 0x30304B50)
            {
                ExpandZipArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0x34327243) // "Cr24" (Chrome 扩展)
            {
                ExpandCrxArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0x4B434150) // "PACK" (Unreal 资源包)
            {
                ExpandPakArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0x4643534D) // "MSCF" (CAB 压缩包)
            {
                ExpandCabArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0xAFBC7A37) // "7z\xBC\xAF" (7-Zip)
            {
                Expand7zArchive(item, buffer, mainVm);
            }
            else if (buffer.ReadByte(item.Node.Offset) == 0x1F && buffer.ReadByte(item.Node.Offset + 1) == 0x8B && buffer.ReadByte(item.Node.Offset + 2) == 0x08)
            {
                mainVm.StatusText = "GZip 为压缩格式，不支持展开（.tar.gz 需要 LZ77 解压）";
            }
            else if (buffer.Length >= 263 && buffer.ReadByte(item.Node.Offset + 257) == 0x75 &&
                     buffer.ReadByte(item.Node.Offset + 258) == 0x73 &&
                     buffer.ReadByte(item.Node.Offset + 259) == 0x74 &&
                     buffer.ReadByte(item.Node.Offset + 260) == 0x61 &&
                     buffer.ReadByte(item.Node.Offset + 261) == 0x72) // "ustar" @ offset 257
            {
                ExpandTarArchive(item, buffer, mainVm);
            }
            else if (sig32 == 0x21726152) // "Rar!"
            {
                var rarVer = buffer.ReadByte(item.Node.Offset + 6);
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
        var numDisk = buffer.ReadUInt16(eocdOff + 4, true);
        var numDiskCd = buffer.ReadUInt16(eocdOff + 6, true);
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

                // 读文件名（bit 11=1 时 UTF-8）
                string fileName;
                if (nameLen > 0)
                {
                    var nameBytes = buffer.ReadBytes(cdPos + 46, nameLen);
                    var hasUtf8 = (flags & (1 << 11)) != 0;
                    fileName = hasUtf8
                        ? System.Text.Encoding.UTF8.GetString(nameBytes)
                        : System.Text.Encoding.GetEncoding(0).GetString(nameBytes);
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
                var diskStart = buffer.ReadUInt16(cdPos + 34, true);
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
            var startOffset = item.Node.Offset;
            long pos = startOffset + 8; // 跳过 "Rar!\x1A\x07\x01\x00" 标记(8字节)
            int count = 0;
            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();

            while (pos + 8 <= buffer.Length && count < 10000)
            {
                // CRC32 (4 bytes, we just skip it)
                // HeaderSize (vint) → header 总大小
                var hdrBytes = ReadRar5Block(buffer, pos, out int hdrSize);


                int off = 4; // skip CRC32
                var (hdrSizeVal, hdrSizeLen) = ReadRar5Vint(hdrBytes, off);
                off += hdrSizeLen;

                var (hdrType, typeLen) = ReadRar5Vint(hdrBytes, off);
                off += typeLen;

                var (hdrFlags, flagsLen) = ReadRar5Vint(hdrBytes, off);
                off += flagsLen;
                var hdrFlagsInt = (int)hdrFlags;

                ulong dataAreaSize = 0;
                ulong extraAreaSize = 0;
                if ((hdrFlagsInt & 0x0001) != 0) // ExtraAreaSize
                {
                    var (es, esLen) = ReadRar5Vint(hdrBytes, off);
                    off += esLen;
                    extraAreaSize = es;
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
                    long bodyEnd = hdrBytes.Length - (long)extraAreaSize;
                    if (off < bodyEnd)
                    {
                        // RAR5 文件名是 body 中最后一个可打印 ASCII/UTF-8 片段，
                        // 文件头里 ftime 后可能有 data_version/CRC32/host_os/name_len 等可选字段
                        var bodyLen = (int)(bodyEnd - off);
                        var body = new byte[bodyLen];
                        Array.Copy(hdrBytes, off, body, 0, bodyLen);

                        // 从右向左找最后一个可打印字符区域作为文件名
                        int nameEnd = bodyLen;
                        while (nameEnd > 0 && body[nameEnd - 1] < 0x20) nameEnd--;
                        int nameStart = nameEnd;
                        while (nameStart > 0 && body[nameStart - 1] >= 0x20) nameStart--;

                        if (nameStart < nameEnd && nameEnd - nameStart <= 512)
                            fileName = System.Text.Encoding.UTF8.GetString(body, nameStart, nameEnd - nameStart);
                        else
                            fileName = $"(file_{count})";
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
                    }
                }
                else if (hdrTypeInt == 5) // 结束块

                pos += hdrSize + (long)dataAreaSize;
                if (pos >= buffer.Length) break;
            }

            if (count > 0)
            {
                BuildArchiveTree(item.Node, entries, mainVm);
                return count;
            }

            // 后备方案：扫描文件名模式
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

        /// <summary>展开 Chrome 扩展包 (CRX) — 计算 ZIP 数据偏移后复用 ZIP 展开逻辑</summary>
        private void ExpandCrxArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            if (buffer.Length < 16) { mainVm.StatusText = "CRX 文件头部不完整"; return; }

            var version = buffer.ReadUInt32(4, true);
            long zipDataOffset;

            if (version == 2)
            {
                var pubKeyLen = buffer.ReadUInt32(8, true);
                var sigLen = buffer.ReadUInt32(12, true);
                zipDataOffset = 16L + pubKeyLen + sigLen;
            }
            else if (version == 3)
            {
                var headerLen = buffer.ReadUInt32(8, true);
                zipDataOffset = 12L + headerLen;
            }
            else
            {
                mainVm.StatusText = $"不支持的 CRX 版本: {version}";
                return;
            }

            if (zipDataOffset >= buffer.Length) { mainVm.StatusText = "CRX 头部超出文件范围"; return; }

            // 验证 ZIP 魔数
            if (zipDataOffset + 4 > buffer.Length || buffer.ReadUInt32(zipDataOffset, true) != 0x04034B50)
            {
                mainVm.StatusText = "CRX 中未找到有效的 ZIP 数据";
                return;
            }

            // 复用 ZIP 展开逻辑，但所有偏移需要加上 zipDataOffset
            ExpandZipArchiveWithOffset(item, buffer, mainVm, zipDataOffset);
        }

        /// <summary>带偏移量的 ZIP 展开 — EOCD/Central Directory/Local Header 读取时增加 baseOffset</summary>
        private void ExpandZipArchiveWithOffset(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm, long baseOffset)
        {
            // 扫描整体文件尾部找 EOCD（EOCD 始终在文件绝对末端）
            var eocdOff = FindEocd(buffer);
            if (eocdOff < 0) { mainVm.StatusText = "无法定位 ZIP 目录"; return; }

            var totalEntries = buffer.ReadUInt16(eocdOff + 8, true);
            var cdRelOffset = buffer.ReadUInt32(eocdOff + 16, true);
            var cdOffset = baseOffset + cdRelOffset; // 调整：加上 ZIP 数据起始偏移
            if (totalEntries == 0) { mainVm.StatusText = "压缩包为空"; return; }

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
                if (isEncrypted) hasEncrypted = true;

                var compMethod = buffer.ReadUInt16(cdPos + 10, true);
                var compSize = buffer.ReadUInt32(cdPos + 20, true);
                var uncompSize = buffer.ReadUInt32(cdPos + 24, true);
                var nameLen = buffer.ReadUInt16(cdPos + 28, true);
                var extraLen = buffer.ReadUInt16(cdPos + 30, true);
                var commentLen = buffer.ReadUInt16(cdPos + 32, true);
                var localHeaderRelOff = buffer.ReadUInt32(cdPos + 42, true);
                var localHeaderOff = baseOffset + localHeaderRelOff; // 调整

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

                long dataOffset = 0;
                var methodStr = compMethod switch { 0 => "Stored", 8 => "Deflate", 12 => "BZIP2", _ => $"M{compMethod}" };

                if (localHeaderOff + 30 <= buffer.Length)
                {
                    var localFnLen = buffer.ReadUInt16(localHeaderOff + 26, true);
                    var localExtraLen = buffer.ReadUInt16(localHeaderOff + 28, true);
                    dataOffset = localHeaderOff + 30 + localFnLen + localExtraLen;
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

        /// <summary>展开 Unreal Engine 资源包 (PAK) — 解析文件索引列举所有条目</summary>
        private void ExpandPakArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            if (buffer.Length < 20) { mainVm.StatusText = "PAK 文件头部不完整"; return; }

            var version = buffer.ReadUInt32(4, true);

            // 扫描尾部查找 "PACK" 魔数
            long trailerOffset = -1;
            for (long i = buffer.Length - 8; i >= buffer.Length - 64 && i >= 0; i--)
            {
                if (buffer.ReadUInt32(i, true) == 0x4B434150)
                { trailerOffset = i; break; }
            }
            if (trailerOffset < 0) { mainVm.StatusText = "无法定位 PAK 尾部"; return; }

            // 读 indexOffset
            long indexOffset;
            if (version >= 9) // UE5
            {
                if (trailerOffset - 16 < 0) return;
                indexOffset = (long)buffer.ReadUInt64(trailerOffset - 16, true);
            }
            else
            {
                if (trailerOffset - 8 < 0) return;
                indexOffset = (long)buffer.ReadUInt64(trailerOffset - 8, true);
            }
            if (indexOffset < 0 || indexOffset >= buffer.Length) { mainVm.StatusText = "无效的 PAK 索引偏移"; return; }

            // 读 MountPoint
            long pos = indexOffset;
            string mountPoint = "";
            if (pos + 4 <= buffer.Length)
            {
                var mountLen = buffer.ReadInt32(pos, true);
                if (mountLen > 0 && mountLen < 1024 && pos + 4 + mountLen <= buffer.Length)
                {
                    mountPoint = System.Text.Encoding.UTF8.GetString(buffer.ReadBytes(pos + 4, mountLen));
                    pos += 4 + mountLen;
                }
                else pos += 4;
            }

            // 读文件数量
            long fileCount;
            if (version >= 9) { if (pos + 8 > buffer.Length) return; fileCount = (long)buffer.ReadUInt64(pos, true); pos += 8; }
            else { if (pos + 4 > buffer.Length) return; fileCount = buffer.ReadUInt32(pos, true); pos += 4; }

            if (fileCount <= 0 || fileCount > 1000000) { mainVm.StatusText = "PAK 文件数量异常"; return; }

            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();
            long lastGoodOffset = pos;
            for (int i = 0; i < fileCount && pos + 4 <= buffer.Length; i++)
            {
                var fnLen = buffer.ReadInt32(pos, true);
                if (fnLen < 0 || fnLen > 1024) break;
                if (pos + 4 + fnLen + 40 > buffer.Length) break;

                var fileName = System.Text.Encoding.UTF8.GetString(buffer.ReadBytes(pos + 4, fnLen));
                pos += 4 + fnLen;

                var fileOffset = (long)buffer.ReadUInt64(pos, true); pos += 8;
                var compressedSize = (long)buffer.ReadUInt64(pos, true); pos += 8;
                var uncompressedSize = (long)buffer.ReadUInt64(pos, true); pos += 8;
                var compressionMethod = buffer.ReadUInt32(pos, true); pos += 4;

                var methodStr = compressionMethod switch
                {
                    0 => "None", 1 => "Zlib", 2 => "Gzip",
                    3 => "Oodle", 4 => "LZ4",
                    _ => $"M{compressionMethod}",
                };
                var desc = $"{methodStr}: {compressedSize} → {uncompressedSize} bytes";
                var displayName = $"{fileName}  [{methodStr}]  {FormatSize(uncompressedSize)}";
                entries.Add((fileOffset, compressedSize, displayName, desc, fileName, false));
                pos += 20; // skip hash (20 bytes SHA1)
                lastGoodOffset = pos;

                if (entries.Count >= 50000) break; // 安全上限
            }

            if (entries.Count > 0)
            {
                mainVm.StatusText = $"PAK 展开完成，共 {entries.Count} 个条目 (mount: {mountPoint})";
                BuildArchiveTree(item.Node, entries, mainVm);
            }
            else
            {
                mainVm.StatusText = "PAK 中未找到有效条目";
            }
        }

        /// <summary>展开 CAB 压缩包 — 解析 CFFILE 条目列表</summary>
        private void ExpandCabArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            if (buffer.Length < 36) { mainVm.StatusText = "CAB 文件头部不完整"; return; }

            var filesOffset = buffer.ReadUInt32(12, true);
            var numFiles = buffer.ReadUInt16(24, true);
            var numFolders = buffer.ReadUInt16(22, true);

            if (numFiles <= 0 || filesOffset < 36 || filesOffset >= buffer.Length)
            { mainVm.StatusText = "无效的 CAB 文件索引"; return; }

            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();
            long pos = filesOffset;

            for (int i = 0; i < numFiles && pos + 8 <= buffer.Length; i++)
            {
                var fileSize = buffer.ReadUInt32(pos, true);
                var fileOffset = buffer.ReadUInt32(pos + 4, true);
                var folderIndex = buffer.ReadUInt16(pos + 8, true);

                // 查找 null 终止的文件名（在 pos+14 之后）
                var nameStart = pos + 14; // skip size(4)+offset(4)+folderIdx(2)+date(2)+time(2)+flags(2)=14
                if (nameStart >= buffer.Length) break;

                var maxLen = (int)Math.Min(buffer.Length - nameStart, 256);
                var nameBytes = buffer.ReadBytes(nameStart, maxLen);
                var nullIdx = Array.IndexOf<byte>(nameBytes, 0);
                string fileName;
                if (nullIdx > 0)
                {
                    fileName = System.Text.Encoding.ASCII.GetString(nameBytes, 0, nullIdx);
                    pos = nameStart + nullIdx + 1;
                }
                else
                {
                    fileName = $"(file_{i})";
                    pos = nameStart + maxLen;
                }

                var displayName = $"{fileName}  {FormatSize(fileSize)}";
                var desc = $"文件夹[{folderIndex}] offset=0x{fileOffset:X}, size={fileSize}";
                entries.Add((fileOffset, fileSize, displayName, desc, fileName, false));

                if (entries.Count >= 50000) break;
            }

            if (entries.Count > 0)
            {
                mainVm.StatusText = $"CAB 展开完成，共 {entries.Count} 个文件 (文件夹: {numFolders})";
                BuildArchiveTree(item.Node, entries, mainVm);
            }
            else
            {
                mainVm.StatusText = "CAB 中未找到有效文件条目";
            }
        }

        /// <summary>展开 7z 压缩包 — 解析 Start Header 和 Next Header</summary>
        private void Expand7zArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            if (buffer.Length < 32) { mainVm.StatusText = "7z 文件头部不完整"; return; }

            long baseOffset = item.Node.Offset;
            bool isMultiVolume = mainVm.VolumeList.Count > 1 && mainVm.IsVolumeListVisible;

            // ── Step 1: 读 SignatureHeader ──
            var majorVer = buffer.ReadByte(baseOffset + 6);
            var minorVer = buffer.ReadByte(baseOffset + 7);
            var nextOff = (long)buffer.ReadUInt64(baseOffset + 12, true);
            var nextSize = (long)buffer.ReadUInt64(baseOffset + 20, true);

            if (nextOff == 0 && nextSize == 0) { mainVm.StatusText = "7z 文件中无附加头（空归档？）"; return; }

            // ── Step 2: 定位并读取 NextHeader ──
            long nhAbsolute = baseOffset + 32 + nextOff;
            byte[] nhData;

            // 多卷：NextHeader 在尾部卷
            if (isMultiVolume && nhAbsolute >= buffer.Length)
            {
                var volPaths = mainVm.VolumeList.Select(v => v.FullPath)
                    .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                if (volPaths.Count < 2) { mainVm.StatusText = "多卷信息不完整"; return; }

                // 从最后卷尾部读取 NextHeader
                var lastVolPath = volPaths[^1];
                if (!File.Exists(lastVolPath)) { mainVm.StatusText = $"未找到尾卷: {Path.GetFileName(lastVolPath)}"; return; }

                BinaryBuffer? lastBuf = null;
                try
                {
                    lastBuf = BinaryBuffer.LoadFromFile(lastVolPath);
                    // NextHeader 总是在最后卷的尾部——重新计算局部偏移
                    long localNhStart = lastBuf.Length - nextSize;
                    if (localNhStart < 0 || nextSize <= 0)
                    { mainVm.StatusText = "尾卷中未找到 NextHeader"; return; }

                    nhData = lastBuf.ReadBytes(localNhStart, (int)nextSize);
                }
                finally { lastBuf?.Dispose(); }
            }
            else
            {
                // 单卷：直接读取
                if (nhAbsolute + nextSize > buffer.Length)
                { mainVm.StatusText = "7z Next Header 超出文件范围"; return; }

                var readSize = (int)Math.Min(nextSize, 1024 * 1024);
                nhData = buffer.ReadBytes(nhAbsolute, readSize);
            }

            // ── Step 3: 用 NID 解析器解析 NextHeader ──
            var parser = new SevenZipHeaderParser();
            var parseResult = parser.Parse(nhData);

            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();

            if (parseResult.HeaderIsCompressed)
            {
                // ── 压缩头 (kEncodedHeader)：尝试 LZMA 解压获取文件列表 ──
                if (parseResult.PackStreams.Count > 0 && parseResult.LzmaProperties != null &&
                    parseResult.HeaderUnpackedSize > 0 && parseResult.HeaderUnpackedSize < 1024 * 1024)
                {
                    var ps = parseResult.PackStreams[0];
                    long compressedOffset = baseOffset + 32 + ps.PackPos;
                    byte[]? compressedData = null;

                    if (isMultiVolume)
                    {
                        // 多卷：压缩头数据在尾卷
                        var volPaths = mainVm.VolumeList.Select(v => v.FullPath)
                            .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                        if (volPaths.Count > 1)
                        {
                            var lastVol = volPaths[^1];
                            if (File.Exists(lastVol))
                            {
                                // 压缩数据在尾卷局部位置
                                long totalPrev = 0;
                                for (int vi = 0; vi < volPaths.Count - 1; vi++)
                                    try { totalPrev += new FileInfo(volPaths[vi]).Length; } catch { }
                                long localOff = compressedOffset - totalPrev;
                                BinaryBuffer? lastBuf2 = null;
                                try
                                {
                                    lastBuf2 = BinaryBuffer.LoadFromFile(lastVol);
                                    if (localOff >= 0 && localOff + ps.PackSize <= lastBuf2.Length)
                                        compressedData = lastBuf2.ReadBytes(localOff, (int)ps.PackSize);
                                }
                                finally { lastBuf2?.Dispose(); }
                            }
                        }
                    }
                    else
                    {
                        if (compressedOffset >= 0 && compressedOffset + ps.PackSize <= buffer.Length)
                            compressedData = buffer.ReadBytes(compressedOffset, (int)ps.PackSize);
                    }

                    if (compressedData != null)
                    {
                        try
                        {
                            var decompressed = SevenZipLzmaDecoder.Decompress(
                                parseResult.LzmaProperties, compressedData, parseResult.HeaderUnpackedSize);

                            var innerParser = new SevenZipHeaderParser();
                            var innerResult = innerParser.Parse(decompressed);

                            if (innerResult.Files.Count > 0)
                            {
                                // 从解压头的 MainStreamsInfo 获取数据流偏移基线
                                long dataBase = baseOffset + 32;
                                if (innerResult.PackStreams.Count > 0)
                                    dataBase += innerResult.PackStreams[0].PackPos;

                                // 从 SubStreamsInfo 获取每个子流的解压大小
                                var subSizes = innerResult.SubStreamUnpackSizes;
                                // 计算最后一个子流大小 = 总解压大小 - 已列出大小之和
                                long totalUnpack = innerResult.HeaderUnpackedSize;
                                long sumExplicit = subSizes.Sum();
                                if (sumExplicit < totalUnpack && subSizes.Count > 0)
                                    subSizes.Add(totalUnpack - sumExplicit);

                                long cumulativeOff = 0;
                                int sizeIdx = 0;
                                // 最后一个子流的大小（如果文件数超过子流数，多余文件共享此大小）
                                long lastSubSize = subSizes.Count > 0 ? subSizes[^1] : 1;
                                long lastSubOff = 0;
                                bool lastSubUsed = false;

                                string globalMethod = innerResult.CompressionMethods ?? parseResult.CompressionMethods ?? "LZMA";
                                for (int i = 0; i < innerResult.Files.Count; i++)
                                {
                                    var f = innerResult.Files[i];
                                    if (f.IsEmptyStream || string.IsNullOrEmpty(f.Name))
                                    {
                                        if (!string.IsNullOrEmpty(f.Name) && f.Name.EndsWith("/"))
                                            entries.Add((0, 0, $"{f.Name}  [-]  -", "目录条目", f.Name.TrimEnd('/'), false));
                                        continue;
                                    }
                                    // 空文件不消耗子流索引。多余的非空文件共享最后一个子流
                                    long fileLen, fileOff;
                                    if (sizeIdx < subSizes.Count)
                                    {
                                        fileLen = subSizes[sizeIdx];
                                        fileOff = dataBase + cumulativeOff;
                                        cumulativeOff += fileLen;
                                        sizeIdx++;
                                        if (sizeIdx == subSizes.Count)
                                        { lastSubOff = fileOff; lastSubUsed = true; }
                                    }
                                    else
                                    {
                                        // 此文件与上一个文件共享最后一个子流
                                        fileOff = lastSubOff;
                                        fileLen = lastSubSize;
                                    }
                                    if (fileLen <= 0) fileLen = 1;

                                    string sharedNote = sizeIdx > subSizes.Count ? " [共享子流]" : "";
                                    string methodStr = string.IsNullOrEmpty(f.CompressionMethod) ? globalMethod : f.CompressionMethod;
                                    string encMark = f.IsEncrypted ? "[加密] " : "";
                                    string sizeStr = FormatSize(fileLen) + sharedNote;
                                    entries.Add((fileOff, fileLen,
                                        $"{f.Name}  [{methodStr}]  {sizeStr}",
                                        $"{encMark}{methodStr}: {FormatSize(fileLen)} @ 0x{fileOff:X}",
                                        f.Name, f.IsEncrypted || (innerResult.IsEncrypted && !f.IsEncrypted)));
                                }
                                parseResult = innerResult;
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }

                // 若 LZMA 解压未产出文件，降级展示 StreamsInfo
                if (entries.Count == 0)
                {
                    mainVm.StatusText = $"7z (v{majorVer}.{minorVer:00}) 头为 LZMA 压缩" +
                        (parseResult.NumFiles > 0 ? $"，含 {parseResult.NumFiles} 个文件（需 LZMA 解压元数据）" : "，暂无法展开文件列表");

                    if (parseResult.PackStreams.Count > 0)
                    {
                        long totalPacked = 0;
                        foreach (var ps in parseResult.PackStreams) totalPacked += ps.PackSize;
                        long packPos = parseResult.PackStreams[0].PackPos;
                        entries.Add((baseOffset + 32 + packPos, totalPacked,
                            $"⚠ 编码头 (LZMA)  {FormatSize(totalPacked)}",
                            $"{(parseResult.IsEncrypted ? "[加密] " : "")}{parseResult.PackStreams.Count} 个包裹流, {totalPacked} bytes → {parseResult.HeaderUnpackedSize} bytes  (文件元数据在 LZMA 压缩块内)",
                            "__encoded_header__", parseResult.IsEncrypted));
                    }
                }
            }
            else if (parseResult.Files.Count > 0)
            {
                // ── 普通头：提取文件名、方法、大小、加密信息 ──
                long dataBaseOffset = baseOffset + 32;
                if (parseResult.PackStreams.Count > 0)
                    dataBaseOffset += parseResult.PackStreams[0].PackPos;

                string globalMethod = parseResult.CompressionMethods ?? "LZMA";

                for (int i = 0; i < parseResult.Files.Count; i++)
                {
                    var f = parseResult.Files[i];
                    if (f.IsEmptyStream || string.IsNullOrEmpty(f.Name))
                    {
                        if (!string.IsNullOrEmpty(f.Name) && f.Name.EndsWith("/"))
                            entries.Add((0, 0, $"{f.Name}  [-]  -", "目录条目", f.Name.TrimEnd('/'), false));
                        continue;
                    }
                    long fileDataOff = i < parseResult.PackStreams.Count
                        ? baseOffset + 32 + parseResult.PackStreams[i].PackPos : dataBaseOffset;
                    if (parseResult.IsEncrypted && !f.IsEncrypted) f.IsEncrypted = parseResult.IsEncrypted;
                    string methodStr = string.IsNullOrEmpty(f.CompressionMethod) ? globalMethod : f.CompressionMethod;
                    string encMark = f.IsEncrypted ? "[加密] " : "";
                    string sizeStr = f.UnpackedSize > 0 ? FormatSize(f.UnpackedSize) : "?";
                    string volSuffix = f.VolumeIndex > 0 ? $" [卷 {f.VolumeIndex + 1}]" : "";
                    entries.Add((fileDataOff, f.PackedSize > 0 ? f.PackedSize : f.UnpackedSize,
                        $"{f.Name}  [{methodStr}]  {sizeStr}{volSuffix}",
                        $"{encMark}{methodStr}: {(f.PackedSize > 0 ? FormatSize(f.PackedSize) : "?")}{(f.UnpackedSize > 0 ? $" → {FormatSize(f.UnpackedSize)}" : "")}{(f.VolumeIndex > 0 ? $" (分卷 {f.VolumeIndex + 1})" : "")}",
                        f.Name, f.IsEncrypted));
                }
            }

            // ── Step 5: 构建树节点 ──
            if (entries.Count > 0)
            {
                mainVm.StatusText = $"7z 展开完成 (v{majorVer}.{minorVer:00}), 共 {entries.Count} 个条目" +
                    (parseResult.IsEncrypted ? " [检测到加密]" : "");
                BuildArchiveTree(item.Node, entries, mainVm);

                if (mainVm.VolumeList.Count > 0 && mainVm.IsVolumeListVisible)
                    TryMapVolumesFor7z(entries, parseResult, mainVm);
            }
            else if (parseResult.NumFiles > 0)
            {
                mainVm.StatusText = $"7z (v{majorVer}.{minorVer:00}) 含 {parseResult.NumFiles} 个文件" +
                    "（元数据为压缩格式，暂无法列出文件名）";
            }
            else
            {
                mainVm.StatusText = $"7z (v{majorVer}.{minorVer:00}) 文件 (NextHeader size={nextSize})";
            }

            if (parseResult.ErrorMessage != null)
                mainVm.StatusText += $" | {parseResult.ErrorMessage}";
        }

        /// <summary>7z 分卷映射：将 PackStream 位置映射到分卷索引</summary>
        private static void TryMapVolumesFor7z(
            List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)> entries,
            SevenZipParseResult parseResult, MainViewModel mainVm)
        {
            try
            {
                // 通过 VolumeList 获取分卷路径（VolumeListItem.FullPath）
                var volPaths = mainVm.VolumeList
                    .Select(v => v.FullPath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .ToList();
                if (volPaths.Count < 2) return;

                var volSizes = new List<long>();
                foreach (var v in volPaths)
                {
                    try
                    {
                        using var volBuf = BinaryBuffer.LoadFromFile(v);
                        volSizes.Add(volBuf.Length);
                    }
                    catch { volSizes.Add(0); }
                }

                // 为每个 PackStream 确定所在卷
                for (int si = 0; si < parseResult.PackStreams.Count && si < volPaths.Count; si++)
                {
                    var ps = parseResult.PackStreams[si];
                    long absPos = ps.PackPos;
                    long remaining = ps.PackSize;

                    for (int vi = 0; vi < volSizes.Count && remaining > 0; vi++)
                    {
                        if (absPos < volSizes[vi])
                        {
                            // 此流（或此 chunk）在卷 vi
                            long chunk = Math.Min(remaining, volSizes[vi] - absPos);
                            ps.VolumeIndex = vi;
                            remaining -= chunk;
                            absPos += chunk;
                        }
                        else
                        {
                            absPos -= volSizes[vi];
                        }
                    }
                }

                // 更新条目显示（只更新非首卷的标记）
                // 条目和 PackStream 的对应关系已在 Expand7zArchive 中处理
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>展开 TAR 归档 — 顺序读取 512B 块</summary>
        private void ExpandTarArchive(TreeItemViewModel item, BinaryBuffer buffer, MainViewModel mainVm)
        {
            if (buffer.Length < 512) { mainVm.StatusText = "TAR 文件头部不完整"; return; }

            var entries = new List<(long dataOffset, long length, string displayName, string description, string path, bool isEncrypted)>();
            long pos = 0;

            while (pos + 512 <= buffer.Length)
            {
                // 零块 = 结束
                bool isZero = true;
                for (int i = 0; i < 512; i++)
                    if (buffer.ReadByte(pos + i) != 0) { isZero = false; break; }
                if (isZero) break;

                var nameBytes = buffer.ReadBytes(pos, 100);
                var fileName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var typeFlag = buffer.ReadByte(pos + 156);
                var sizeStr = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes(pos + 124, 12)).TrimEnd('\0');
                long fileSize = sizeStr.Length > 0 ? System.Convert.ToInt64(sizeStr, 8) : 0;

                if (typeFlag == 0 || typeFlag == '0' || typeFlag == '7') // normal file
                {
                    var roundedSize = ((fileSize + 511) / 512) * 512;
                    var displayName = $"{fileName}  {FormatSize(fileSize)}";
                    entries.Add((pos + 512, fileSize, displayName, $"TAR: {fileName}, size={fileSize}", fileName, false));
                    pos += 512 + roundedSize;
                }
                else // directory or other type
                {
                    pos += 512 + ((fileSize + 511) / 512) * 512;
                }

                if (entries.Count >= 50000) break;
            }

            if (entries.Count > 0)
            {
                mainVm.StatusText = $"TAR 展开完成，共 {entries.Count} 个文件";
                BuildArchiveTree(item.Node, entries, mainVm);
            }
            else
            {
                mainVm.StatusText = "TAR 中未找到有效文件条目";
            }
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
            if (mainVm.StructureTree.RootNode != null)
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
