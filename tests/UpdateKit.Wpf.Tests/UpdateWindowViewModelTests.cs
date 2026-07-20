using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UpdateKit.Desktop.Internal;
using UpdateKit.Wpf.Tests.TestInfrastructure;

namespace UpdateKit.Wpf.Tests;

public sealed class UpdateWindowViewModelTests
{
    [Fact]
    public void InitialState_IsBindableAndReadyToCheck()
    {
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"));

        Assert.Equal(UpdateWindowStatus.Initial, viewModel.Status);
        Assert.Equal("Check for updates", viewModel.Heading);
        Assert.Equal("No release selected", viewModel.ReleaseName);
        Assert.Equal("—", viewModel.AvailableVersion);
        Assert.True(viewModel.CanCheck);
        Assert.True(viewModel.CanClose);
        Assert.False(viewModel.CanDownload);
        Assert.False(viewModel.CanCancel);
        Assert.False(viewModel.IsOperationInProgress);
        Assert.False(viewModel.IsViewReleaseVisible);
        Assert.False(viewModel.CanViewRelease);
        Assert.True(viewModel.CheckForUpdateCommand.CanExecute(null));
        Assert.False(viewModel.DownloadCommand.CanExecute(null));
        Assert.False(viewModel.CancelCommand.CanExecute(null));
        Assert.False(viewModel.ViewReleaseCommand.CanExecute(null));
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsReleaseDetailsAndSelectedAsset()
    {
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = RespondWithRelease("v2.0.0", assetUrl, size: 2_048);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"));
        var changedProperties = new ConcurrentBag<string?>();
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        var started = await viewModel.CheckForUpdateAsync();

        Assert.True(started);
        Assert.Equal(UpdateWindowStatus.UpdateAvailable, viewModel.Status);
        Assert.True(viewModel.CanDownload);
        Assert.Equal("UpdateKit 2.0.0", viewModel.ReleaseName);
        Assert.Equal("v2.0.0", viewModel.AvailableVersion);
        Assert.Equal("Release notes", viewModel.ReleaseNotes);
        Assert.NotEqual("—", viewModel.PublishedText);
        Assert.Equal("UpdateKit.zip (2.0 KB)", viewModel.SelectedAssetText);
        Assert.True(viewModel.IsViewReleaseVisible);
        Assert.True(viewModel.CanViewRelease);
        Assert.True(viewModel.ViewReleaseCommand.CanExecute(null));
        Assert.Contains(nameof(UpdateWindowViewModel.Status), changedProperties);
        Assert.Contains(nameof(UpdateWindowViewModel.ReleaseNotes), changedProperties);
    }

    [Fact]
    public async Task ViewReleaseCommand_UsesInjectedLauncherWithoutChangingWorkflowState()
    {
        var handler = RespondWithRelease(
            "v2.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var launcher = new RecordingReleasePageLauncher(ReleasePageLaunchResult.Success());
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"),
            launcher);
        await viewModel.CheckForUpdateAsync();

        viewModel.ViewReleaseCommand.Execute(null);

        Assert.Equal(
            "https://github.com/octocat/Hello-World/releases/tag/v2.0.0",
            launcher.LaunchedUri?.AbsoluteUri);
        Assert.Equal(UpdateWindowStatus.UpdateAvailable, viewModel.Status);
        Assert.True(viewModel.CanDownload);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task ViewReleaseCommand_PresentsLaunchFailureWithoutDisablingDownload()
    {
        var handler = RespondWithRelease(
            "v2.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var launcher = new RecordingReleasePageLauncher(
            ReleasePageLaunchResult.Failure("The browser could not be started."));
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"),
            launcher);
        await viewModel.CheckForUpdateAsync();

        viewModel.ViewReleaseCommand.Execute(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("browser could not be started", viewModel.ErrorText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UpdateWindowStatus.UpdateAvailable, viewModel.Status);
        Assert.True(viewModel.CanDownload);
        Assert.Null(viewModel.LastError);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsNoUpdateAndDoesNotRestart()
    {
        var handler = RespondWithRelease(
            "v1.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"));

        Assert.True(await viewModel.CheckForUpdateAsync());
        Assert.False(await viewModel.CheckForUpdateAsync());

        Assert.Equal(UpdateWindowStatus.NoUpdate, viewModel.Status);
        Assert.False(viewModel.CheckResult?.IsUpdateAvailable);
        Assert.Null(viewModel.SelectedAsset);
        Assert.True(viewModel.CanClose);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsAssetSelectionError()
    {
        var handler = RespondWithRelease(
            "v2.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = CreateClient(httpClient);
        var options = new UpdateWindowOptions(
            client,
            "1.0.0",
            "unused.zip",
            release => client.SelectAssetByExactName(release, "missing.zip"));
        using var viewModel = new UpdateWindowViewModel(options);

        await viewModel.CheckForUpdateAsync();

        Assert.Equal(UpdateWindowStatus.Failed, viewModel.Status);
        Assert.Equal(UpdateErrorCode.AssetNotFound, viewModel.LastError?.Code);
        Assert.True(viewModel.HasError);
        Assert.Contains("No release asset", viewModel.ErrorText, StringComparison.Ordinal);
        Assert.False(viewModel.CanDownload);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsNetworkErrorAndAllowsRetry()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"));

        await viewModel.CheckForUpdateAsync();

        Assert.Equal(UpdateWindowStatus.Failed, viewModel.Status);
        Assert.Equal(UpdateErrorCode.NetworkError, viewModel.LastError?.Code);
        Assert.True(viewModel.CanCheck);
        Assert.True(viewModel.CanClose);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreventsDuplicatesAndSafeCloseCancels()
    {
        var handler = new BlockingHttpMessageHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, "unused.zip"));

        var firstCheck = viewModel.CheckForUpdateAsync();
        await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(await viewModel.CheckForUpdateAsync());
        Assert.False(viewModel.RequestClose());
        Assert.Equal(UpdateWindowStatus.Canceling, viewModel.Status);
        Assert.False(viewModel.CanClose);

        Assert.True(await firstCheck);
        Assert.Equal(UpdateWindowStatus.Canceled, viewModel.Status);
        Assert.Equal(UpdateErrorCode.DownloadCanceled, viewModel.LastError?.Code);
        Assert.True(viewModel.RequestClose());
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_ReportsProgressAndPresentsSuccess()
    {
        var payload = Enumerable.Range(0, 4_096).Select(index => (byte)(index % 251)).ToArray();
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = RouteReleaseAndAsset("v2.0.0", assetUrl, payload);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, destinationPath));
        var progressValues = new ConcurrentBag<long>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdateWindowViewModel.DownloadProgress) &&
                viewModel.DownloadProgress is { } progress)
            {
                progressValues.Add(progress.BytesDownloaded);
            }
        };

        await viewModel.CheckForUpdateAsync();
        var started = await viewModel.DownloadAsync();

        Assert.True(started);
        Assert.Equal(UpdateWindowStatus.Succeeded, viewModel.Status);
        Assert.Equal(100d, viewModel.ProgressPercentage);
        Assert.NotNull(viewModel.DownloadResult);
        Assert.Contains(progressValues, value => value > 0);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.False(viewModel.CanDownload);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_CancellationPreservesExistingFileAndAllowsRetry()
    {
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var blockingStream = new BlockingReadStream();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.Host == "api.github.com")
            {
                return Task.FromResult(JsonResponse(CreateReleasePayload("v2.0.0", assetUrl, 100)));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(blockingStream),
            });
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "existing version");
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, destinationPath));

        await viewModel.CheckForUpdateAsync();
        var downloadTask = viewModel.DownloadAsync();
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.CancelOperation());
        await downloadTask;

        Assert.Equal(UpdateWindowStatus.Canceled, viewModel.Status);
        Assert.Equal(UpdateErrorCode.DownloadCanceled, viewModel.LastError?.Code);
        Assert.True(viewModel.CanDownload);
        Assert.Equal("existing version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PresentsHttpFailureAndAllowsRetry()
    {
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = new StubHttpMessageHandler((request, _) =>
            Task.FromResult(
                request.RequestUri?.Host == "api.github.com"
                    ? JsonResponse(CreateReleasePayload("v2.0.0", assetUrl, 100))
                    : new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        using var viewModel = new UpdateWindowViewModel(
            CreateWindowOptions(httpClient, directory.GetPath("UpdateKit.zip")));

        await viewModel.CheckForUpdateAsync();
        await viewModel.DownloadAsync();

        Assert.Equal(UpdateWindowStatus.Failed, viewModel.Status);
        Assert.Equal(UpdateErrorCode.DownloadFailed, viewModel.LastError?.Code);
        Assert.True(viewModel.CanDownload);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_DirectChecksumMismatchRemovesDownload()
    {
        var payload = Encoding.UTF8.GetBytes("mismatched payload");
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = RouteReleaseAndAsset("v2.0.0", assetUrl, payload);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var client = CreateClient(httpClient);
        var options = new UpdateWindowOptions(
            client,
            "1.0.0",
            destinationPath,
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ExpectedSha256 = new string('0', 64),
        };
        using var viewModel = new UpdateWindowViewModel(options);

        await viewModel.CheckForUpdateAsync();
        await viewModel.DownloadAsync();

        Assert.Equal(UpdateWindowStatus.Failed, viewModel.Status);
        Assert.Equal(UpdateErrorCode.ChecksumMismatch, viewModel.LastError?.Code);
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public async Task DownloadAsync_SupportsHostSelectedChecksumFile()
    {
        var payload = Encoding.UTF8.GetBytes("verified payload");
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var checksumUrl = new Uri("https://downloads.example.test/checksums.sha256");
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.Host == "api.github.com")
            {
                return Task.FromResult(JsonResponse(CreateReleasePayload(
                    "v2.0.0",
                    assetUrl,
                    payload.LongLength,
                    checksumUrl)));
            }

            return Task.FromResult(request.RequestUri == assetUrl
                ? BytesResponse(payload)
                : TextResponse($"{Checksum(payload)}  UpdateKit.zip"));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var client = CreateClient(httpClient);
        var options = new UpdateWindowOptions(
            client,
            "1.0.0",
            destinationPath,
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ChecksumAssetSelector = release =>
                client.SelectAssetByExactName(release, "checksums.sha256"),
        };
        using var viewModel = new UpdateWindowViewModel(options);

        await viewModel.CheckForUpdateAsync();
        await viewModel.DownloadAsync();

        Assert.Equal(UpdateWindowStatus.Succeeded, viewModel.Status);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public void Options_RejectConflictingVerificationModesAndEmptyTitle()
    {
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var client = CreateClient(httpClient);
        var conflicting = new UpdateWindowOptions(
            client,
            "1.0.0",
            "unused.zip",
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ExpectedSha256 = new string('0', 64),
            ChecksumAssetSelector = release =>
                client.SelectAssetByExactName(release, "checksums.sha256"),
        };
        var emptyTitle = new UpdateWindowOptions(
            client,
            "1.0.0",
            "unused.zip",
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            WindowTitle = " ",
        };

        Assert.Throws<ArgumentException>(() => new UpdateWindowViewModel(conflicting));
        Assert.Throws<ArgumentException>(() => new UpdateWindowViewModel(emptyTitle));
    }

    private static UpdateWindowOptions CreateWindowOptions(
        HttpClient httpClient,
        string destinationFilePath)
    {
        var client = CreateClient(httpClient);
        return new UpdateWindowOptions(
            client,
            "1.0.0",
            destinationFilePath,
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"));
    }

    private static UpdateClient CreateClient(HttpClient httpClient) =>
        new(
            httpClient,
            new UpdateClientOptions
            {
                RepositoryOwner = "octocat",
                RepositoryName = "Hello-World",
            });

    private static StubHttpMessageHandler RespondWithRelease(
        string tag,
        Uri assetUrl,
        long size = 1_024) =>
        new((_, _) => Task.FromResult(JsonResponse(CreateReleasePayload(tag, assetUrl, size))));

    private static StubHttpMessageHandler RouteReleaseAndAsset(
        string tag,
        Uri assetUrl,
        byte[] payload) =>
        new((request, _) => Task.FromResult(
            request.RequestUri?.Host == "api.github.com"
                ? JsonResponse(CreateReleasePayload(tag, assetUrl, payload.LongLength))
                : BytesResponse(payload)));

    private static object CreateReleasePayload(
        string tag,
        Uri assetUrl,
        long size,
        Uri? checksumUrl = null)
    {
        var assets = new List<object>
        {
            CreateAssetPayload("UpdateKit.zip", assetUrl, size),
        };

        if (checksumUrl is not null)
        {
            assets.Add(CreateAssetPayload("checksums.sha256", checksumUrl, 100));
        }

        return new
        {
            id = 42,
            tag_name = tag,
            name = $"UpdateKit {tag.TrimStart('v')}",
            body = "Release notes",
            html_url = $"https://github.com/octocat/Hello-World/releases/tag/{tag}",
            published_at = new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero),
            prerelease = false,
            draft = false,
            assets,
        };
    }

    private static object CreateAssetPayload(string name, Uri url, long size) =>
        new
        {
            name,
            browser_download_url = url.AbsoluteUri,
            size,
            content_type = "application/octet-stream",
        };

    private static HttpResponseMessage JsonResponse(params object[] releases) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(releases),
                Encoding.UTF8,
                "application/json"),
        };

    private static HttpResponseMessage BytesResponse(byte[] payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        };

    private static HttpResponseMessage TextResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain"),
        };

    private static string Checksum(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload));

    private sealed class RecordingReleasePageLauncher : IReleasePageLauncher
    {
        private readonly ReleasePageLaunchResult _result;

        public RecordingReleasePageLauncher(ReleasePageLaunchResult result)
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

    private static StubHttpMessageHandler HandlerThatMustNotRun() =>
        new((_, _) => throw new InvalidOperationException("The HTTP handler must not be called."));

    private static void AssertNoTemporaryFiles(TemporaryDirectory directory) =>
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".updatekit-*.tmp"));

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _requestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            _requestStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
