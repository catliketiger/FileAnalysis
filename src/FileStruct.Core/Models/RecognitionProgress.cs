namespace FileStruct.Core.Models;

/// <summary>
/// 识别进度报告
/// </summary>
public readonly struct RecognitionProgress
{
    public RecognitionProgress(double percentage, string statusText,
        string? currentSubOperation = null)
    {
        Percentage = percentage;
        StatusText = statusText;
        CurrentSubOperation = currentSubOperation;
    }

    public double Percentage { get; }
    public string StatusText { get; }
    public string? CurrentSubOperation { get; }
}
