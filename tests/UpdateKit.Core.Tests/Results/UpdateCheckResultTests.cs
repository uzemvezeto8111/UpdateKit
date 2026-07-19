namespace UpdateKit.Core.Tests.Results;

public sealed class UpdateCheckResultTests
{
    [Fact]
    public void UpdateAvailable_CreatesAvailableResult()
    {
        var release = TestData.Release();

        var result = UpdateCheckResult.UpdateAvailable("1.0.0", release);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Same(release, result.LatestRelease);
    }

    [Fact]
    public void NoUpdate_CreatesUnavailableResult()
    {
        var release = TestData.Release();

        var result = UpdateCheckResult.NoUpdate("1.2.3", release);

        Assert.False(result.IsUpdateAvailable);
        Assert.Same(release, result.LatestRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Factories_RejectMissingCurrentVersion(string? currentVersion)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => UpdateCheckResult.NoUpdate(currentVersion!, TestData.Release()));
    }

    [Fact]
    public void Factories_RejectNullRelease()
    {
        Assert.Throws<ArgumentNullException>(
            () => UpdateCheckResult.UpdateAvailable("1.0.0", null!));
    }
}
