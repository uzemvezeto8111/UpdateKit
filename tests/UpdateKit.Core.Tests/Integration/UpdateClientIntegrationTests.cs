using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UpdateKit.Core.Tests.Http;
using UpdateKit.Core.Tests.IO;
using UpdateKit.GitHub;

namespace UpdateKit.Core.Tests.Integration;

public sealed class UpdateClientIntegrationTests
{
    [Fact]
    public void Constructor_RejectsNullDependenciesAndInvalidOptions()
    {
        using var httpClient = new HttpClient(HandlerThatMustNotRun());

        Assert.Throws<ArgumentNullException>(() => new UpdateClient(null!, CreateOptions()));
        Assert.Throws<ArgumentNullException>(() => new UpdateClient(httpClient, null!));
        Assert.Throws<UpdateConfigurationException>(
            () => new UpdateClient(httpClient, new UpdateClientOptions()));
    }

    [Fact]
    public async Task CheckForUpdateAsync_SelectsHighestSemanticVersion()
    {
        var handler = RespondWithReleases(
            CreateReleasePayload(1, "v1.5.0"),
            CreateReleasePayload(2, "v3.0.0"),
            CreateReleasePayload(3, "v2.4.0"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync("v1.0.0");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsUpdateAvailable);
        Assert.Equal("v1.0.0", result.Value.CurrentVersion);
        Assert.Equal("v3.0.0", result.Value.LatestRelease.TagName);
        Assert.Equal(2, result.Value.LatestRelease.Id);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.0.0+local.5")]
    [InlineData("3.0.0")]
    public async Task CheckForUpdateAsync_ReturnsNoUpdateForEqualOrNewerCurrentVersion(
        string currentVersion)
    {
        var handler = RespondWithReleases(CreateReleasePayload(1, "v2.0.0+release.1"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync(currentVersion);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsUpdateAvailable);
        Assert.Equal(currentVersion, result.Value.CurrentVersion);
        Assert.Equal("v2.0.0+release.1", result.Value.LatestRelease.TagName);
    }

    [Fact]
    public async Task CheckForUpdateAsync_KeepsFirstReleaseForEqualPrecedence()
    {
        var handler = RespondWithReleases(
            CreateReleasePayload(1, "v2.0.0+first"),
            CreateReleasePayload(2, "2.0.0+second"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync("1.0.0");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.LatestRelease.Id);
        Assert.Equal("v2.0.0+first", result.Value.LatestRelease.TagName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("V1.0.0")]
    public async Task CheckForUpdateAsync_MapsInvalidCurrentVersionWithoutRequest(
        string? currentVersion)
    {
        var handler = HandlerThatMustNotRun();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync(currentVersion);

        AssertError(result, UpdateErrorCode.InvalidVersion);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CheckForUpdateAsync_MapsInvalidEligibleReleaseTag()
    {
        var handler = RespondWithReleases(
            CreateReleasePayload(1, "v2.0.0"),
            CreateReleasePayload(2, "release-latest"));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync("1.0.0");

        AssertError(result, UpdateErrorCode.InvalidVersion);
    }

    [Theory]
    [InlineData(false, "v1.5.0")]
    [InlineData(true, "v2.0.0-beta.1")]
    public async Task CheckForUpdateAsync_PreservesDraftAndPrereleaseFiltering(
        bool includePrereleases,
        string expectedTag)
    {
        var handler = RespondWithReleases(
            CreateReleasePayload(1, "v1.5.0"),
            CreateReleasePayload(2, "v2.0.0-beta.1", isPrerelease: true),
            CreateReleasePayload(3, "v9.0.0", isDraft: true));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(
            httpClient,
            CreateOptions(includePrereleases: includePrereleases));

        var result = await client.CheckForUpdateAsync("1.0.0");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsUpdateAvailable);
        Assert.Equal(expectedTag, result.Value.LatestRelease.TagName);
        Assert.False(result.Value.LatestRelease.IsDraft);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PropagatesGitHubFailureResult()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());

        var result = await client.CheckForUpdateAsync("1.0.0");

        AssertError(result, UpdateErrorCode.RepositoryNotFound);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PropagatesCallerCancellation()
    {
        var handler = HandlerThatMustNotRun();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CheckForUpdateAsync("1.0.0", cancellationSource.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void AssetSelectionMethods_DelegateEstablishedSelectionContracts()
    {
        var firstZip = TestData.Asset("UpdateKit-x64.ZIP");
        var installer = TestData.Asset("UpdateKit-setup.exe");
        var release = CreateRelease("v2.0.0", firstZip, installer);
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var client = new UpdateClient(httpClient, CreateOptions());

        var exact = client.SelectAssetByExactName(release, "UpdateKit-setup.exe");
        var extension = client.SelectAssetByExtension(release, ".zip");
        var predicate = client.SelectAssetByPredicate(
            release,
            asset => asset.Name.Contains("x64", StringComparison.Ordinal));
        var missing = client.SelectAssetByExactName(release, "missing.zip");

        Assert.Same(installer, exact.Value);
        Assert.Same(firstZip, extension.Value);
        Assert.Same(firstZip, predicate.Value);
        AssertError(missing, UpdateErrorCode.AssetNotFound);
    }

    [Fact]
    public async Task CheckSelectAndDownload_ComposesFullUnverifiedFlowWithProgress()
    {
        var payload = Encoding.UTF8.GetBytes("downloaded release payload");
        var assetUrl = new Uri("https://downloads.example.test/UpdateKit.zip");
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.Host == "api.github.com")
            {
                return Task.FromResult(
                    JsonResponse(
                        CreateReleasePayload(
                            1,
                            "v2.0.0",
                            assets: [CreateAssetPayload("UpdateKit.zip", assetUrl, payload.LongLength)])));
            }

            Assert.Equal(assetUrl, request.RequestUri);
            return Task.FromResult(BytesResponse(payload));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var progress = new RecordingProgress();

        var check = await client.CheckForUpdateAsync("1.0.0");
        var selectedAsset = client.SelectAssetByExactName(
            check.Value.LatestRelease,
            "UpdateKit.zip");
        var download = await client.DownloadAsync(
            selectedAsset.Value,
            destinationPath,
            progress);

        Assert.True(check.Value.IsUpdateAvailable);
        Assert.True(download.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(payload.LongLength, download.Value.BytesDownloaded);
        Assert.Equal(payload.LongLength, progress.Updates[^1].BytesDownloaded);
        Assert.Equal(payload.LongLength, progress.Updates[^1].TotalBytes);
        Assert.Equal(2, handler.CallCount);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsCanceledAndPreservesExistingDestination()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "existing version");
        var blockingStream = new BlockingReadStream();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(blockingStream),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var cancellationSource = new CancellationTokenSource();

        var downloadTask = client.DownloadAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath,
            cancellationToken: cancellationSource.Token);
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await downloadTask;

        AssertError(result, UpdateErrorCode.DownloadCanceled);
        Assert.Equal("existing version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_VerifiesDirectChecksumAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("directly verified payload");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse(payload)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var result = await client.DownloadAndVerifyAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath,
            Checksum(payload));

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAndVerifyFromChecksumFileAsync_VerifiesReleaseChecksumFile()
    {
        var payload = Encoding.UTF8.GetBytes("checksum-file verified payload");
        var asset = CreateAsset(
            "UpdateKit portable.zip",
            new Uri("https://downloads.example.test/UpdateKit-portable.zip"),
            payload.LongLength);
        var checksumAsset = CreateAsset(
            "checksums.sha256",
            new Uri("https://downloads.example.test/checksums.sha256"));
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == asset.DownloadUrl)
            {
                return Task.FromResult(BytesResponse(payload));
            }

            Assert.Equal(checksumAsset.DownloadUrl, request.RequestUri);
            return Task.FromResult(
                TextResponse($"{Checksum(payload)} *{asset.Name}"));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("renamed-download.zip");

        var result = await client.DownloadAndVerifyFromChecksumFileAsync(
            asset,
            destinationPath,
            checksumAsset);

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_DeletesDownloadedFileOnChecksumMismatch()
    {
        var payload = Encoding.UTF8.GetBytes("mismatched payload");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse(payload)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var result = await client.DownloadAndVerifyAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath,
            new string('0', 64));

        AssertError(result, UpdateErrorCode.ChecksumMismatch);
        Assert.False(File.Exists(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAndVerifyFromChecksumFileAsync_PreservesDownloadWhenEntryIsMissing()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        var asset = CreateAsset(
            "UpdateKit.zip",
            new Uri("https://downloads.example.test/UpdateKit.zip"),
            payload.LongLength);
        var checksumAsset = CreateAsset(
            "checksums.sha256",
            new Uri("https://downloads.example.test/checksums.sha256"));
        var handler = new StubHttpMessageHandler((request, _) =>
            Task.FromResult(
                request.RequestUri == asset.DownloadUrl
                    ? BytesResponse(payload)
                    : TextResponse($"{Checksum(payload)}  other.zip")));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var result = await client.DownloadAndVerifyFromChecksumFileAsync(
            asset,
            destinationPath,
            checksumAsset);

        AssertError(result, UpdateErrorCode.ChecksumNotFound);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
    }

    [Fact]
    public async Task DownloadAsync_PropagatesHttpFailureWithoutCreatingDestination()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, CreateOptions());
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var result = await client.DownloadAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.False(File.Exists(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PropagatesFilesystemCommitFailureAndPreservesDestination()
    {
        var payload = Encoding.UTF8.GetBytes("new version");
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "existing version");
        var expected = new IOException("Commit failed.");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse(payload)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var downloader = new AssetDownloader(
            httpClient,
            bufferSize: 4,
            commitFile: (_, _) => throw expected);
        var client = new UpdateClient(
            new StaticReleaseSource(TestData.Release()),
            downloader,
            new Sha256Verifier(httpClient));

        var result = await client.DownloadAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal("existing version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_PropagatesVerificationFilesystemFailure()
    {
        var payload = Encoding.UTF8.GetBytes("downloaded payload");
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var expected = new IOException("Verification read failed.");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse(payload)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        Func<string, Stream> openFile = _ => throw expected;
        var verifier = new Sha256Verifier(httpClient, openFile, File.Delete);
        var client = new UpdateClient(
            new StaticReleaseSource(TestData.Release()),
            new AssetDownloader(httpClient),
            verifier);

        var result = await client.DownloadAndVerifyAsync(
            CreateAsset("UpdateKit.zip", new Uri("https://downloads.example.test/UpdateKit.zip")),
            destinationPath,
            Checksum(payload));

        AssertError(result, UpdateErrorCode.FileSystemError);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
    }

    private static UpdateClientOptions CreateOptions(bool includePrereleases = false) =>
        new()
        {
            RepositoryOwner = "octocat",
            RepositoryName = "Hello-World",
            IncludePrereleases = includePrereleases,
        };

    private static StubHttpMessageHandler RespondWithReleases(params object[] releases) =>
        new((request, _) =>
        {
            Assert.Equal("api.github.com", request.RequestUri?.Host);
            return Task.FromResult(JsonResponse(releases));
        });

    private static HttpResponseMessage JsonResponse(params object[] releases) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(releases),
                Encoding.UTF8,
                "application/json"),
        };

    private static object CreateReleasePayload(
        long id,
        string tagName,
        bool isPrerelease = false,
        bool isDraft = false,
        object[]? assets = null) =>
        new
        {
            id,
            tag_name = tagName,
            name = $"UpdateKit {tagName}",
            body = "Release notes",
            html_url = $"https://github.com/octocat/Hello-World/releases/tag/{tagName}",
            published_at = new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero),
            prerelease = isPrerelease,
            draft = isDraft,
            assets = assets ??
                [CreateAssetPayload(
                    "UpdateKit.zip",
                    new Uri("https://downloads.example.test/UpdateKit.zip"),
                    1_024)],
        };

    private static object CreateAssetPayload(string name, Uri downloadUrl, long size) =>
        new
        {
            name,
            browser_download_url = downloadUrl.AbsoluteUri,
            size,
            content_type = "application/zip",
        };

    private static ReleaseInfo CreateRelease(
        string tagName,
        params ReleaseAsset[] assets) =>
        new(
            42,
            tagName,
            $"UpdateKit {tagName}",
            "Release notes",
            new Uri("https://example.test/releases/42"),
            DateTimeOffset.UtcNow,
            false,
            false,
            assets);

    private static ReleaseAsset CreateAsset(string name, Uri downloadUrl, long size = 1_024) =>
        new(name, downloadUrl, size, "application/octet-stream");

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

    private static void AssertError<T>(UpdateResult<T> result, UpdateErrorCode expectedCode)
        where T : notnull
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static void AssertNoTemporaryFiles(TemporaryDirectory directory) =>
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".updatekit-*.tmp"));

    private sealed class RecordingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Updates { get; } = [];

        public void Report(DownloadProgress value) => Updates.Add(value);
    }

    private sealed class StaticReleaseSource : IGitHubReleaseSource
    {
        private readonly IReadOnlyList<ReleaseInfo> _releases;

        public StaticReleaseSource(params ReleaseInfo[] releases)
        {
            _releases = releases;
        }

        public Task<UpdateResult<IReadOnlyList<ReleaseInfo>>> GetReleasesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                UpdateResult<IReadOnlyList<ReleaseInfo>>.Success(_releases));
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
