using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.ViewLinkage;

/// <summary>
/// 视图联动服务：处理结构树与 HexView 之间的双向跳转和高亮
/// </summary>
public class ViewLinkageService
{
    private readonly ILogService _logger;

    public ViewLinkageService(ILogService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根据偏移查找最匹配的结构树节点
    /// </summary>
    public StructureNode? FindNodeByOffset(StructureNode root, long offset)
    {
        if (root == null) return null;
        using var op = _logger.BeginOperation($"查找偏移 {offset:X} 对应节点");

        // 先尝试精确匹配
        var exact = root.FindByOffset(offset);
        if (exact != null && exact != root) return exact;

        // 无精确匹配时找最近的容器节点
        return FindClosestContainer(root, offset);
    }

    /// <summary>
    /// 计算跳转到目标节点所需的 HexView 滚动偏移
    /// </summary>
    public long CalculateScrollOffset(StructureNode node, int bytesPerRow = 16)
    {
        // 滚动到节点起始位置所在的行的起始处
        return (node.Offset / bytesPerRow) * bytesPerRow;
    }

    /// <summary>
    /// 检查 HexView 选择范围是否匹配某个节点
    /// </summary>
    public StructureNode? MatchSelectionToNode(StructureNode root, long selectionStart, long selectionLength)
    {
        if (root == null || selectionLength <= 0) return null;

        var selectionEnd = selectionStart + selectionLength - 1;
        return FindNodeByRange(root, selectionStart, selectionEnd);
    }

    /// <summary>
    /// 为节点生成一组高亮范围（子节点区域不同颜色）
    /// </summary>
    public List<HighlightRange> GetHighlightRanges(StructureNode node)
    {
        var ranges = new List<HighlightRange>();
        CollectHighlights(node, ranges, 0);
        return ranges;
    }

    private static StructureNode? FindClosestContainer(StructureNode node, long offset)
    {
        if (offset < node.Offset || offset >= node.Offset + node.Length)
            return null;

        foreach (var child in node.Children)
        {
            var found = FindClosestContainer(child, offset);
            if (found != null) return found;
        }

        return node;
    }

    private static StructureNode? FindNodeByRange(StructureNode node, long start, long end)
    {
        if (node.Offset > end || node.Offset + node.Length <= start)
            return null;

        foreach (var child in node.Children)
        {
            var found = FindNodeByRange(child, start, end);
            if (found != null) return found;
        }

        return node;
    }

    private static void CollectHighlights(StructureNode node, List<HighlightRange> ranges, int depth)
    {
        var color = depth switch
        {
            0 => "#40E3F2FD", // 最浅蓝
            1 => "#40C8E6C9", // 浅绿
            2 => "#40FFF9C4", // 浅黄
            3 => "#40FFCCBC", // 浅橙
            _ => "#40E1BEE7", // 浅紫
        };

        ranges.Add(new HighlightRange
        {
            StartOffset = node.Offset,
            EndOffset = node.Offset + node.Length,
            Color = color,
            NodeName = node.Name,
            Depth = depth,
        });

        foreach (var child in node.Children)
        {
            CollectHighlights(child, ranges, depth + 1);
        }
    }
}

/// <summary>
/// 高亮范围定义
/// </summary>
public class HighlightRange
{
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public string Color { get; set; } = "#40E3F2FD";
    public string NodeName { get; set; } = "";
    public int Depth { get; set; }
}
