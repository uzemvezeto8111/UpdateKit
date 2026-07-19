namespace UpdateKit.Core.Tests.Results;

public sealed class UpdateResultTests
{
    [Fact]
    public void Success_ExposesOnlyValue()
    {
        var value = TestData.Release();

        var result = UpdateResult<ReleaseInfo>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Same(value, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_ExposesOnlyError()
    {
        var error = new UpdateError(UpdateErrorCode.NetworkError, "The request failed.");

        var result = UpdateResult<ReleaseInfo>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_RejectsNullValue()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateResult<string>.Success(null!));
    }

    [Fact]
    public void Failure_RejectsNullError()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateResult<string>.Failure(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UpdateError_RejectsMissingMessage(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new UpdateError(UpdateErrorCode.Unknown, message!));
    }
}
