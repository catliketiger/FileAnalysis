using FileStruct.Core.Exceptions;
using FileLoadException = FileStruct.Core.Exceptions.FileLoadException;

namespace FileStruct.Core.Tests.Exceptions;

public class ExceptionTests
{
    [Fact]
    public void FileLoadException_HasUserAction()
    {
        var ex = new FileLoadException("文件不存在", "/path/to/file");
        Assert.Contains("权限", ex.UserAction ?? "");
        Assert.True(ex.ContextInfo.ContainsKey("FilePath"));
        Assert.Equal("/path/to/file", ex.ContextInfo["FilePath"]);
    }

    [Fact]
    public void FileTooLargeException_HasFileSizeInfo()
    {
        var ex = new FileTooLargeException(300 * 1024 * 1024);
        Assert.Contains("超过上限", ex.Message);
        Assert.Contains("200MB", ex.UserAction ?? "");
        Assert.Equal(300L * 1024 * 1024, ex.FileSize);
        Assert.Equal(200L * 1024 * 1024, ex.MaxSize);
        Assert.True(ex.ContextInfo.ContainsKey("FileSize"));
    }

    [Fact]
    public void FileCorruptedException_HasUserAction()
    {
        var ex = new FileCorruptedException("文件截断", "/path/to/file");
        Assert.Contains("损坏", ex.UserAction ?? "");
        Assert.True(ex.ContextInfo.ContainsKey("FilePath"));
    }

    [Fact]
    public void RecognitionFailedException_HasUserAction()
    {
        var ex = new RecognitionFailedException("未知格式");
        Assert.Contains("尚未被支持", ex.UserAction ?? "");
        Assert.Equal("未知格式", ex.Reason);
        Assert.True(ex.ContextInfo.ContainsKey("Reason"));
    }

    [Fact]
    public void FormatRuleConflictException_HasUserAction()
    {
        var ex = new FormatRuleConflictException("NewRule", "ExistingRule", "签名重叠");
        Assert.Contains("冲突", ex.UserAction ?? "");
        Assert.Equal("NewRule", ex.RuleName);
        Assert.Equal("ExistingRule", ex.ExistingRuleName);
    }

    [Fact]
    public void ProjectVersionMismatchException_HasUserAction()
    {
        var ex = new ProjectVersionMismatchException("1.5", "1.3");
        Assert.Contains("版本", ex.UserAction ?? "");
        Assert.Equal("1.5", ex.ExpectedVersion);
        Assert.Equal("1.3", ex.ActualVersion);
    }

    [Fact]
    public void FileStructException_StoresMultipleContextEntries()
    {
        var ex = new FileLoadException("测试", "/test/path");
        ex.ContextInfo["Extra"] = "test-value";

        Assert.Equal("/test/path", ex.ContextInfo["FilePath"]);
        Assert.Equal("test-value", ex.ContextInfo["Extra"]);
    }
}
