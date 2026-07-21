using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UpdateKit.WinForms.Tests.TestInfrastructure;

namespace UpdateKit.WinForms.Tests;

public sealed class UpdateDialogControllerTests
{
    [Fact]
    public void InitialState_HasExpectedActionsAndNoReleaseData()
    {
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        var state = controller.State;

        Assert.Equal(UpdateDialogStatus.Initial, state.Status);
        Assert.True(state.CanCheck);
        Assert.True(state.CanClose);
        Assert.False(state.CanDownload);
        Assert.False(state.CanCancel);
        Assert.False(state.IsBusy);
        Assert.Null(state.CheckResult);
        Assert.Null(state.SelectedAsset);
        Assert.Null(state.Error);
        Assert.Contains("does not install or run", UpdateDialog.DownloadSafetyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadAsync_ExistingDestinationDirectoryUsesSelectedAssetFilename()
    {
        var payload = Encoding.UTF8.GetBytes("generic MSI payload");
        var assetUrl = new Uri("https://downloads.example.test/My Product Setup.msi");
        var handler = RouteReleaseAndAsset(
            "v2.0.0",
            assetUrl,
            payload,
            "My Product Setup.msi");
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var client = CreateClient(httpClient);
        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            directory.Path,
            release => client.SelectAssetByExtension(release, ".msi"));
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();
        await controller.DownloadAsync();

        var expectedPath = directory.GetPath("My Product Setup.msi");
        Assert.Equal(UpdateDialogStatus.Succeeded, controller.State.Status);
        Assert.Equal("My Product Setup.msi", controller.State.SelectedAsset?.Name);
        Assert.Equal(expectedPath, controller.State.DownloadResult?.FilePath);
        Assert.Equal(payload, await File.ReadAllBytesAsync(expectedPath));
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsUpdateAndSelectedAsset()
    {
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = RespondWithRelease("v2.0.0", assetUrl, size: 2_048);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        var started = await controller.CheckForUpdateAsync();
        var state = controller.State;

        Assert.True(started);
        Assert.Equal(UpdateDialogStatus.UpdateAvailable, state.Status);
        Assert.True(state.CanDownload);
        Assert.True(state.CanClose);
        Assert.Equal("v2.0.0", state.CheckResult?.LatestRelease.TagName);
        Assert.Equal("UpdateKit 2.0.0", state.CheckResult?.LatestRelease.Name);
        Assert.Equal("Release notes", state.CheckResult?.LatestRelease.Body);
        Assert.NotNull(state.CheckResult?.LatestRelease.PublishedAt);
        Assert.Equal("UpdateKit.zip", state.SelectedAsset?.Name);
        Assert.Equal(2_048, state.SelectedAsset?.Size);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsNoUpdateAndDoesNotRestart()
    {
        var handler = RespondWithRelease(
            "v1.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        Assert.True(await controller.CheckForUpdateAsync());
        Assert.False(await controller.CheckForUpdateAsync());
        Assert.False(await controller.DownloadAsync());

        var state = controller.State;
        Assert.Equal(UpdateDialogStatus.NoUpdate, state.Status);
        Assert.False(state.CheckResult?.IsUpdateAvailable);
        Assert.Null(state.SelectedAsset);
        Assert.True(state.CanClose);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsAssetSelectionFailure()
    {
        var handler = RespondWithRelease(
            "v2.0.0",
            new Uri("https://downloads.example.test/UpdateKit.zip"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = CreateClient(httpClient);
        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            "unused.zip",
            release => client.SelectAssetByExactName(release, "missing.zip"));
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();

        var state = controller.State;
        Assert.Equal(UpdateDialogStatus.Failed, state.Status);
        Assert.Equal(UpdateErrorCode.AssetNotFound, state.Error?.Code);
        Assert.NotNull(state.CheckResult);
        Assert.Null(state.SelectedAsset);
        Assert.False(state.CanDownload);
        Assert.True(state.CanClose);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PresentsActionableCoreErrorAndAllowsRetry()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();

        var state = controller.State;
        Assert.Equal(UpdateDialogStatus.Failed, state.Status);
        Assert.Equal(UpdateErrorCode.NetworkError, state.Error?.Code);
        Assert.False(string.IsNullOrWhiteSpace(state.Error?.Message));
        Assert.True(state.CanCheck);
        Assert.True(state.CanClose);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreventsDuplicateConcurrentOperation()
    {
        var handler = new BlockingHttpMessageHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        var firstCheck = controller.CheckForUpdateAsync();
        await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var secondStarted = await controller.CheckForUpdateAsync();

        Assert.False(secondStarted);
        Assert.Equal(UpdateDialogStatus.Checking, controller.State.Status);
        Assert.True(controller.CancelOperation());
        Assert.True(await firstCheck);
        Assert.Equal(UpdateDialogStatus.Canceled, controller.State.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RequestClose_CancelsActiveOperationAndAllowsCloseAfterSettlement()
    {
        var handler = new BlockingHttpMessageHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var options = CreateDialogOptions(httpClient, "unused.zip");
        using var controller = new UpdateDialogController(options);

        var checkTask = controller.CheckForUpdateAsync();
        await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(controller.RequestClose());
        Assert.Equal(UpdateDialogStatus.Canceling, controller.State.Status);
        Assert.False(controller.State.CanClose);

        await checkTask;

        Assert.Equal(UpdateDialogStatus.Canceled, controller.State.Status);
        Assert.True(controller.State.CanClose);
        Assert.True(controller.RequestClose());
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
        var options = CreateDialogOptions(httpClient, destinationPath);
        using var controller = new UpdateDialogController(options);
        var states = new ConcurrentQueue<UpdateDialogViewState>();
        controller.StateChanged += states.Enqueue;

        await controller.CheckForUpdateAsync();
        var started = await controller.DownloadAsync();

        Assert.True(started);
        Assert.Equal(UpdateDialogStatus.Succeeded, controller.State.Status);
        Assert.NotNull(controller.State.DownloadResult);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Contains(
            states,
            state => state.Status == UpdateDialogStatus.Downloading &&
                state.Progress?.BytesDownloaded > 0);
        Assert.True(controller.State.CanClose);
        Assert.False(controller.State.CanDownload);
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
        var options = CreateDialogOptions(httpClient, destinationPath);
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();
        var downloadTask = controller.DownloadAsync();
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(controller.CancelOperation());
        await downloadTask;

        Assert.Equal(UpdateDialogStatus.Canceled, controller.State.Status);
        Assert.Equal(UpdateErrorCode.DownloadCanceled, controller.State.Error?.Code);
        Assert.True(controller.State.CanDownload);
        Assert.Equal("existing version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PresentsDownloadFailureAndAllowsRetry()
    {
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = new StubHttpMessageHandler((request, _) =>
            Task.FromResult(
                request.RequestUri?.Host == "api.github.com"
                    ? JsonResponse(CreateReleasePayload("v2.0.0", assetUrl, 100))
                    : new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var options = CreateDialogOptions(httpClient, directory.GetPath("UpdateKit.zip"));
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();
        await controller.DownloadAsync();

        Assert.Equal(UpdateDialogStatus.Failed, controller.State.Status);
        Assert.Equal(UpdateErrorCode.DownloadFailed, controller.State.Error?.Code);
        Assert.True(controller.State.CanDownload);
        Assert.True(controller.State.CanClose);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PresentsVerificationFailureAndMismatchCleanup()
    {
        var payload = Encoding.UTF8.GetBytes("mismatched payload");
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = RouteReleaseAndAsset("v2.0.0", assetUrl, payload);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var client = CreateClient(httpClient);
        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            destinationPath,
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ExpectedSha256 = new string('0', 64),
        };
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();
        await controller.DownloadAsync();

        Assert.Equal(UpdateDialogStatus.Failed, controller.State.Status);
        Assert.Equal(UpdateErrorCode.ChecksumMismatch, controller.State.Error?.Code);
        Assert.True(controller.State.CanDownload);
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
        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            destinationPath,
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ChecksumAssetSelector = release =>
                client.SelectAssetByExactName(release, "checksums.sha256"),
        };
        using var controller = new UpdateDialogController(options);

        await controller.CheckForUpdateAsync();
        await controller.DownloadAsync();

        Assert.Equal(UpdateDialogStatus.Succeeded, controller.State.Status);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public void Options_RejectConflictingVerificationModes()
    {
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var client = CreateClient(httpClient);
        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            "unused.zip",
            release => client.SelectAssetByExactName(release, "UpdateKit.zip"))
        {
            ExpectedSha256 = new string('0', 64),
            ChecksumAssetSelector = release =>
                client.SelectAssetByExactName(release, "checksums.sha256"),
        };

        Assert.Throws<ArgumentException>(() => new UpdateDialogController(options));
    }

    private static UpdateDialogOptions CreateDialogOptions(
        HttpClient httpClient,
        string destinationFilePath)
    {
        var client = CreateClient(httpClient);
        return new UpdateDialogOptions(
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
        byte[] payload,
        string assetName = "UpdateKit.zip") =>
        new((request, _) => Task.FromResult(
            request.RequestUri?.Host == "api.github.com"
                ? JsonResponse(CreateReleasePayload(tag, assetUrl, payload.LongLength, assetName: assetName))
                : BytesResponse(payload)));

    private static object CreateReleasePayload(
        string tag,
        Uri assetUrl,
        long size,
        Uri? checksumUrl = null,
        string assetName = "UpdateKit.zip")
    {
        var assets = new List<object>
        {
            CreateAssetPayload(assetName, assetUrl, size),
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
