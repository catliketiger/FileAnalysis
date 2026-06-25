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
    }
}
