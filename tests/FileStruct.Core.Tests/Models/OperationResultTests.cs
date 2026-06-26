using FileStruct.Core.Models;

namespace FileStruct.Core.Tests.Models;

public class OperationResultTests
{
    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        var result = OperationResult<int>.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.UserAction);
    }

    [Fact]
    public void Fail_CreatesFailureResult()
    {
        var result = OperationResult<string>.Fail("出错了", "请重试", "ERR001");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("出错了", result.ErrorMessage);
        Assert.Equal("请重试", result.UserAction);
        Assert.Equal("ERR001", result.ErrorCode);
    }

    [Fact]
    public void VoidOk_CreatesSuccessResult()
    {
        var result = OperationResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void VoidFail_CreatesFailureResult()
    {
        var result = OperationResult.Fail("失败", "检查输入");

        Assert.False(result.Success);
        Assert.Equal("失败", result.ErrorMessage);
        Assert.Equal("检查输入", result.UserAction);
    }

    [Fact]
    public void OkWithNull_AllowsNullData()
    {
        var result = OperationResult<object?>.Ok(null);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }
}
