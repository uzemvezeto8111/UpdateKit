using UpdateKit.Desktop.Internal;
using UpdateKit.WinForms.Internal;

namespace UpdateKit.WinForms.Tests;

public sealed class ReleasePageActionTests
{
    [Fact]
    public void Validator_AcceptsCredentialFreeGitHubHttpsReleasePage()
    {
        var candidate = new Uri("https://github.com/octocat/Hello-World/releases/tag/v0.2.1");

        var isSafe = ReleasePageUriValidator.TryGetSafeReleasePageUri(candidate, out var safeUri);

        Assert.True(isSafe);
        Assert.Same(candidate, safeUri);
    }

    [Fact]
    public void Validator_RejectsMissingUrl()
    {
        Assert.False(ReleasePageUriValidator.TryGetSafeReleasePageUri(null, out var safeUri));
        Assert.Null(safeUri);
    }

    [Theory]
    [InlineData("release/tag/v0.2.1")]
    [InlineData("http://github.com/octocat/Hello-World/releases/tag/v0.2.1")]
    [InlineData("https://example.test/octocat/Hello-World/releases/tag/v0.2.1")]
    [InlineData("https://github.com/octocat/Hello-World/releases/download/v0.2.1/UpdateKit.zip")]
    [InlineData("https://github.com/octocat/Hello-World/releases/tag/v0.2.1?access_token=secret")]
    [InlineData("https://user:secret@github.com/octocat/Hello-World/releases/tag/v0.2.1")]
    [InlineData("C:/Downloads/UpdateKit.zip")]
    public void Validator_RejectsMalformedOrUnsafeUrl(string value)
    {
        var candidate = new Uri(value, UriKind.RelativeOrAbsolute);

        Assert.False(ReleasePageUriValidator.TryGetSafeReleasePageUri(candidate, out var safeUri));
        Assert.Null(safeUri);
    }

    [Fact]
    public void Action_PassesOnlyValidatedReleasePageToInjectedLauncher()
    {
        var launcher = new RecordingLauncher(ReleasePageLaunchResult.Success());
        var action = new ReleasePageAction(launcher);
        var releasePage = new Uri("https://github.com/octocat/Hello-World/releases/tag/v0.2.1");

        var result = action.TryOpen(releasePage);

        Assert.True(result.IsSuccess);
        Assert.Same(releasePage, launcher.LaunchedUri);
    }

    [Fact]
    public void Action_ReturnsNonfatalLaunchFailure()
    {
        var expected = ReleasePageLaunchResult.Failure("Browser launch failed.");
        var action = new ReleasePageAction(new RecordingLauncher(expected));

        var result = action.TryOpen(
            new Uri("https://github.com/octocat/Hello-World/releases/tag/v0.2.1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Browser launch failed.", result.ErrorMessage);
    }

    [Fact]
    public void ViewState_ShowsAndEnablesActionOnlyForSafeReleasePageWhenIdle()
    {
        var safeState = CreateState(
            new Uri("https://github.com/octocat/Hello-World/releases/tag/v0.2.1"),
            UpdateDialogStatus.UpdateAvailable);
        var busyState = safeState with { Status = UpdateDialogStatus.Downloading };
        var unsafeState = CreateState(
            new Uri("http://github.com/octocat/Hello-World/releases/tag/v0.2.1"),
            UpdateDialogStatus.UpdateAvailable);

        Assert.True(safeState.IsViewReleaseVisible);
        Assert.True(safeState.CanViewRelease);
        Assert.True(busyState.IsViewReleaseVisible);
        Assert.False(busyState.CanViewRelease);
        Assert.False(unsafeState.IsViewReleaseVisible);
        Assert.False(unsafeState.CanViewRelease);
        Assert.False(UpdateDialogViewState.Initial.IsViewReleaseVisible);
    }

    private static UpdateDialogViewState CreateState(Uri htmlUrl, UpdateDialogStatus status)
    {
        var asset = new ReleaseAsset(
            "UpdateKit.zip",
            new Uri("https://downloads.example.test/UpdateKit.zip"),
            1_024,
            "application/zip");
        var release = new ReleaseInfo(
            42,
            "v0.2.1",
            "UpdateKit 0.2.1",
            "Release notes",
            htmlUrl,
            DateTimeOffset.UtcNow,
            false,
            false,
            [asset]);
        return new UpdateDialogViewState(
            status,
            UpdateCheckResult.UpdateAvailable("0.2.0", release),
            asset);
    }

    private sealed class RecordingLauncher : IReleasePageLauncher
    {
        private readonly ReleasePageLaunchResult _result;

        public RecordingLauncher(ReleasePageLaunchResult result)
        {
            _result = result;
        }

        public Uri? LaunchedUri { get; private set; }

        public ReleasePageLaunchResult Launch(Uri releasePageUri)
        {
            LaunchedUri = releasePageUri;
            return _result;
        }
    }
}
