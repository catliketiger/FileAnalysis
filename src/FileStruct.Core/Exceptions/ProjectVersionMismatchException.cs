namespace FileStruct.Core.Exceptions;

/// <summary>
/// 项目版本不匹配：项目文件由不同版本创建
/// </summary>
public class ProjectVersionMismatchException : FileStructException
{
    public string ExpectedVersion { get; }
    public string ActualVersion { get; }

    public ProjectVersionMismatchException(string expected, string actual)
        : base($"项目文件版本 ({actual}) 与当前版本 ({expected}) 不兼容")
    {
        ExpectedVersion = expected;
        ActualVersion = actual;
        UserAction = "请使用与项目文件相同版本的 FileStruct 打开，或创建新的项目文件";
        ContextInfo["ExpectedVersion"] = expected;
        ContextInfo["ActualVersion"] = actual;
    }
}
