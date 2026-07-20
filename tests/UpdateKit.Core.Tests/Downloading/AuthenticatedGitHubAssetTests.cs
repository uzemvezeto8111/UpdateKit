using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UpdateKit.Core.Tests.Http;
using UpdateKit.Core.Tests.IO;
using UpdateKit.GitHub;
using UpdateKit.Internal;

namespace UpdateKit.Core.Tests.Downloading;

public sealed class AuthenticatedGitHubAssetTests
{
    private const string TestToken = "test-token-not-a-real-credential";
    private const string TestUserAgent = "UpdateKit.Security.Tests";

    [Fact]
    public async Task DownloadAsync_AuthenticatedAssetUsesVerifiedApiUrlAndHeaders()
    {
        var payload = Encoding.UTF8.GetBytes("private release payload");
        var apiUri = AssetApiUri(101);
        var asset = CreateGitHubAsset(apiUri, payload.LongLength);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(apiUri, request.RequestUri);
            AssertGitHubApiHeaders(request);
            return Task.FromResult(BytesResponse(payload));
        });
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            TestToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.Value.FilePath));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RedirectToGitHubCdnDoesNotForwardApiHeadersOrToken()
    {
        var payload = Encoding.UTF8.GetBytes("redirected private payload");
        var apiUri = AssetApiUri(102);
        var cdnUri = new Uri("https://objects.githubusercontent.com/private/UpdateKit.zip");
        var asset = CreateGitHubAsset(apiUri, payload.LongLength);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == apiUri)
            {
                AssertGitHubApiHeaders(request);
                return Task.FromResult(RedirectResponse(cdnUri));
            }

            Assert.Equal(cdnUri, request.RequestUri);
            AssertNoLibraryAuthenticationOrApiHeaders(request);
            return Task.FromResult(BytesResponse(payload));
        });
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            TestToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.Value.FilePath));
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_TokenConfiguredWithoutApiMetadataNeverAuthenticatesBrowserHost()
    {
        var payload = Encoding.UTF8.GetBytes("public payload");
        var browserUri = new Uri("https://downloads.example.test/UpdateKit.zip");
        var asset = new ReleaseAsset("UpdateKit.zip", browserUri, payload.LongLength);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(browserUri, request.RequestUri);
            AssertNoLibraryAuthenticationOrApiHeaders(request);
            return Task.FromResult(BytesResponse(payload));
        });
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            TestToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_UntrustedApiMetadataIsRejectedBeforeAnyRequest()
    {
        var asset = new ReleaseAsset(
            "UpdateKit.zip",
            new Uri("https://downloads.example.test/UpdateKit.zip"),
            10);
        ReleaseAssetMetadata.SetGitHubApiDownloadUri(
            asset,
            new Uri("https://attacker.example.test/repos/octocat/Hello-World/releases/assets/103"));
        var handler = HandlerThatMustNotRun();
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            TestToken);

        AssertError(result, UpdateErrorCode.InvalidConfiguration);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_MissingAuthenticationPreservesPublicBrowserBehavior()
    {
        var browserUri = new Uri("https://github.com/octocat/Hello-World/releases/download/v1.0.0/UpdateKit.zip");
        var asset = CreateGitHubAsset(AssetApiUri(104), 10, browserUri);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(browserUri, request.RequestUri);
            AssertNoLibraryAuthenticationOrApiHeaders(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            accessToken: null);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_AuthenticatedCancellationUsesResultContractAndCleansUp()
    {
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var asset = CreateGitHubAsset(AssetApiUri(105), 10);
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            AssertGitHubApiHeaders(request);
            requestStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return BytesResponse([1]);
        });
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath(asset.Name);
        using var cancellationSource = new CancellationTokenSource();

        var downloadTask = DownloadAsync(
            handler,
            asset,
            destinationPath,
            TestToken,
            cancellationSource.Token);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await downloadTask;

        AssertError(result, UpdateErrorCode.DownloadCanceled);
        Assert.False(File.Exists(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_AuthenticatedRejectionMapsAuthenticationFailure()
    {
        var asset = CreateGitHubAsset(AssetApiUri(106), 10);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            AssertGitHubApiHeaders(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        });
        using var directory = new TemporaryDirectory();

        var result = await DownloadAsync(
            handler,
            asset,
            directory.GetPath(asset.Name),
            TestToken);

        AssertError(result, UpdateErrorCode.AuthenticationFailed);
        Assert.False(File.Exists(directory.GetPath(asset.Name)));
    }

    [Fact]
    public async Task DownloadAsync_AuthenticatedRedirectToPlainHttpIsRejectedWithoutSecondRequest()
    {
        var asset = CreateGitHubAsset(AssetApiUri(107), 10);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            AssertGitHubApiHeaders(request);
            return Task.FromResult(
                RedirectResponse(new Uri("http://downloads.example.test/UpdateKit.zip")));
        });
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath(asset.Name);
        await File.WriteAllTextAsync(destinationPath, "existing version");

        var result = await DownloadAsync(
            handler,
            asset,
            destinationPath,
            TestToken);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("existing version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task UpdateClient_PrivateAssetsUseAuthenticationForDownloadAndChecksumFile()
    {
        var payload = Encoding.UTF8.GetBytes("verified private payload");
        var assetApiUri = AssetApiUri(201);
        var checksumApiUri = AssetApiUri(202);
        var releaseApiUri = new Uri(
            "https://api.github.com/repos/octocat/Hello-World/releases?per_page=100");
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == releaseApiUri)
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal(TestToken, request.Headers.Authorization?.Parameter);
                return Task.FromResult(JsonResponse(CreateReleasePayload(
                    CreateAssetPayload(201, "UpdateKit.zip", assetApiUri, payload.LongLength),
                    CreateAssetPayload(202, "checksums.sha256", checksumApiUri, 100))));
            }

            AssertGitHubApiHeaders(request);
            return Task.FromResult(request.RequestUri == assetApiUri
                ? BytesResponse(payload)
                : TextResponse($"{Convert.ToHexString(SHA256.HashData(payload))}  UpdateKit.zip"));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(
            httpClient,
            new UpdateClientOptions
            {
                RepositoryOwner = "octocat",
                RepositoryName = "Hello-World",
                AccessToken = TestToken,
                UserAgent = TestUserAgent,
            });
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var check = await client.CheckForUpdateAsync("1.0.0");
        var asset = client.SelectAssetByExactName(check.Value.LatestRelease, "UpdateKit.zip");
        var checksumAsset = client.SelectAssetByExactName(
            check.Value.LatestRelease,
            "checksums.sha256");
        var result = await client.DownloadAndVerifyFromChecksumFileAsync(
            asset.Value,
            destinationPath,
            checksumAsset.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task UpdateClient_UntrustedAssetApiUrlMapsMalformedResponseWithoutDownloading()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("api.github.com", request.RequestUri?.Host);
            return Task.FromResult(JsonResponse(CreateReleasePayload(
                CreateAssetPayload(
                    301,
                    "UpdateKit.zip",
                    new Uri("https://attacker.example.test/repos/octocat/Hello-World/releases/assets/301"),
                    10))));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(
            httpClient,
            new UpdateClientOptions
            {
                RepositoryOwner = "octocat",
                RepositoryName = "Hello-World",
                AccessToken = TestToken,
            });

        var result = await client.CheckForUpdateAsync("1.0.0");

        AssertError(result, UpdateErrorCode.MalformedResponse);
        Assert.Equal(1, handler.CallCount);
    }

    private static async Task<UpdateResult<DownloadResult>> DownloadAsync(
        HttpMessageHandler handler,
        ReleaseAsset asset,
        string destinationPath,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var requestClient = new ReleaseAssetRequestClient(
            httpClient,
            accessToken,
            TestUserAgent);
        var downloader = new AssetDownloader(requestClient, bufferSize: 4);
        return await downloader.DownloadAsync(
            asset,
            destinationPath,
            cancellationToken: cancellationToken);
    }

    private static ReleaseAsset CreateGitHubAsset(
        Uri apiUri,
        long size,
        Uri? browserUri = null)
    {
        var asset = new ReleaseAsset(
            "UpdateKit.zip",
            browserUri ?? new Uri(
                "https://github.com/octocat/Hello-World/releases/download/v2.0.0/UpdateKit.zip"),
            size,
            "application/zip");
        ReleaseAssetMetadata.SetGitHubApiDownloadUri(asset, apiUri);
        return asset;
    }

    private static Uri AssetApiUri(long id) =>
        new($"https://api.github.com/repos/octocat/Hello-World/releases/assets/{id}");

    private static void AssertGitHubApiHeaders(HttpRequestMessage request)
    {
        Assert.True(GitHubApiEndpoint.IsReleaseAsset(request.RequestUri!));
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(TestToken, request.Headers.Authorization?.Parameter);
        Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/octet-stream");
        Assert.Equal(TestUserAgent, request.Headers.UserAgent.ToString());
        Assert.Equal(
            GitHubReleaseSource.ApiVersion,
            Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
    }

    private static void AssertNoLibraryAuthenticationOrApiHeaders(HttpRequestMessage request)
    {
        Assert.Null(request.Headers.Authorization);
        Assert.Empty(request.Headers.Accept);
        Assert.Empty(request.Headers.UserAgent);
        Assert.False(request.Headers.Contains("X-GitHub-Api-Version"));
    }

    private static HttpResponseMessage RedirectResponse(Uri location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = location;
        return response;
    }

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

    private static HttpResponseMessage JsonResponse(params object[] releases) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(releases),
                Encoding.UTF8,
                "application/json"),
        };

    private static object CreateReleasePayload(params object[] assets) =>
        new
        {
            id = 42,
            tag_name = "v2.0.0",
            name = "UpdateKit 2.0.0",
            body = "Release notes",
            html_url = "https://github.com/octocat/Hello-World/releases/tag/v2.0.0",
            published_at = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            prerelease = false,
            draft = false,
            assets,
        };

    private static object CreateAssetPayload(long id, string name, Uri apiUri, long size) =>
        new
        {
            id,
            url = apiUri.AbsoluteUri,
            name,
            browser_download_url =
                $"https://github.com/octocat/Hello-World/releases/download/v2.0.0/{name}",
            size,
            content_type = "application/octet-stream",
        };

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
}
