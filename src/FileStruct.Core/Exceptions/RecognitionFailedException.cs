namespace FileStruct.Core.Exceptions;

/// <summary>
/// 结构识别失败：引擎无法处理该文件
/// </summary>
public class RecognitionFailedException : FileStructException
{
    public string Reason { get; }

    public RecognitionFailedException(string reason)
        : base($"结构识别失败: {reason}")
    {
        Reason = reason;
        UserAction = "该文件格式可能尚未被支持，请尝试手动添加格式规则，或稍后使用 AI 识别功能";
        ContextInfo["Reason"] = reason;
    }
}
