using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UpdateKit.Core.Tests.Http;
using UpdateKit.GitHub;

namespace UpdateKit.Core.Tests.GitHub;

public sealed class GitHubReleaseSourceTests
{
    [Fact]
    public async Task GetReleasesAsync_MapsReleaseAndSendsRequiredHeaders()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://api.github.com/repos/octocat/Hello-World/releases?per_page=100",
                request.RequestUri?.AbsoluteUri);
            Assert.Contains(
                request.Headers.Accept,
                value => value.MediaType == "application/vnd.github+json");
            Assert.Equal("UpdateKit", Assert.Single(request.Headers.GetValues("User-Agent")));
            Assert.Equal(
                GitHubReleaseSource.ApiVersion,
                Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
            Assert.Null(request.Headers.Authorization);

            return Task.FromResult(JsonResponse(CreateReleasePayload()));
        });

        var result = await RetrieveAsync(handler);

        Assert.True(result.IsSuccess);
        var release = Assert.Single(result.Value);
        Assert.Equal(42, release.Id);
        Assert.Equal("v1.2.3", release.TagName);
        Assert.Equal("UpdateKit 1.2.3", release.Name);
        Assert.Equal("Release notes", release.Body);
        Assert.Equal(
            new Uri("https://github.com/octocat/Hello-World/releases/tag/v1.2.3"),
            release.HtmlUrl);
        Assert.Equal(new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero), release.PublishedAt);
        Assert.False(release.IsDraft);
        Assert.False(release.IsPrerelease);

        var asset = Assert.Single(release.Assets);
        Assert.Equal("UpdateKit.zip", asset.Name);
        Assert.Equal(
            new Uri("https://github.com/octocat/Hello-World/releases/download/v1.2.3/UpdateKit.zip"),
            asset.DownloadUrl);
        Assert.Equal(1024, asset.Size);
        Assert.Equal("application/zip", asset.ContentType);
    }

    [Fact]
    public async Task GetReleasesAsync_SendsOptionalAccessTokenAsBearerAuthentication()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(JsonResponse(CreateReleasePayload()));
        });
        var options = CreateOptions(accessToken: "secret-token");

        var result = await RetrieveAsync(handler, options);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetReleasesAsync_AlwaysExcludesDraftsBeforeMapping()
    {
        var draftWithNoPublishableFields = new
        {
            draft = true,
            prerelease = false,
        };
        var handler = RespondWithJson(draftWithNoPublishableFields, CreateReleasePayload(tagName: "v1.0.0"));

        var result = await RetrieveAsync(handler, CreateOptions(includePrereleases: true));

        Assert.True(result.IsSuccess);
        Assert.Equal("v1.0.0", Assert.Single(result.Value).TagName);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task GetReleasesAsync_RespectsPrereleaseOption(
        bool includePrereleases,
        int expectedCount)
    {
        var handler = RespondWithJson(
            CreateReleasePayload(id: 43, tagName: "v2.0.0-beta.1", isPrerelease: true),
            CreateReleasePayload(id: 42, tagName: "v1.2.3"));

        var result = await RetrieveAsync(handler, CreateOptions(includePrereleases: includePrereleases));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Value.Count);
        Assert.Equal("v1.2.3", result.Value[^1].TagName);
    }

    [Fact]
    public async Task GetReleasesAsync_ReturnsNoReleaseWhenEveryReleaseIsFiltered()
    {
        var handler = RespondWithJson(
            CreateReleasePayload(id: 43, tagName: "v2.0.0-beta.1", isPrerelease: true),
            CreateReleasePayload(id: 42, tagName: "v1.2.3", isDraft: true));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.NoReleaseFound, result.Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, UpdateErrorCode.AuthenticationFailed)]
    [InlineData(HttpStatusCode.Forbidden, UpdateErrorCode.AuthenticationFailed)]
    [InlineData(HttpStatusCode.NotFound, UpdateErrorCode.RepositoryNotFound)]
    [InlineData(HttpStatusCode.InternalServerError, UpdateErrorCode.NetworkError)]
    public async Task GetReleasesAsync_MapsHttpFailures(
        HttpStatusCode statusCode,
        UpdateErrorCode expectedCode)
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsPrimaryRateLimitAndReportsResetTime()
    {
        const long resetSeconds = 1784455200;
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", resetSeconds.ToString());
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.RateLimitExceeded, result.Error.Code);
        Assert.Contains(DateTimeOffset.FromUnixTimeSeconds(resetSeconds).ToString("O"), result.Error.Message);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsSecondaryRateLimitAndReportsRetryDelay()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.RateLimitExceeded, result.Error.Code);
        Assert.Contains("30 seconds", result.Error.Message);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsSecondaryRateLimitFromGitHubErrorBody()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """
                {
                  "message": "You have exceeded a secondary rate limit.",
                  "documentation_url": "https://docs.github.com/rest/using-the-rest-api/rate-limits-for-the-rest-api"
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.RateLimitExceeded, result.Error.Code);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsInvalidJsonToMalformedResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.MalformedResponse, result.Error.Code);
        Assert.IsType<JsonException>(result.Error.Exception);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsMissingRequiredReleaseFieldsToMalformedResponse()
    {
        var incompleteRelease = new
        {
            id = 42,
            draft = false,
            prerelease = false,
        };
        var handler = RespondWithJson(incompleteRelease);

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.MalformedResponse, result.Error.Code);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsInvalidAssetToMalformedResponse()
    {
        var invalidAsset = CreateAssetPayload(size: -1);
        var handler = RespondWithJson(CreateReleasePayload(assets: [invalidAsset]));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.MalformedResponse, result.Error.Code);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsHttpRequestExceptionToNetworkError()
    {
        var exception = new HttpRequestException("Network unavailable");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(exception));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.NetworkError, result.Error.Code);
        Assert.Same(exception, result.Error.Exception);
    }

    [Fact]
    public async Task GetReleasesAsync_MapsRequestTimeoutToNetworkError()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation token was not honored.");
        });
        var options = CreateOptions(requestTimeout: TimeSpan.FromMilliseconds(25));

        var result = await RetrieveAsync(handler, options);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.NetworkError, result.Error.Code);
        Assert.IsAssignableFrom<OperationCanceledException>(result.Error.Exception);
    }

    [Fact]
    public async Task GetReleasesAsync_PropagatesCallerCancellation()
    {
        var handler = RespondWithJson(CreateReleasePayload());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RetrieveAsync(handler, cancellationToken: cancellationSource.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetReleasesAsync_FollowsTrustedPaginationLinks()
    {
        var requestedUris = new List<string>();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri!.AbsoluteUri);

            if (requestedUris.Count == 1)
            {
                var firstPage = JsonResponse(CreateReleasePayload(id: 43, tagName: "v2.0.0"));
                firstPage.Headers.TryAddWithoutValidation(
                    "Link",
                    "<https://api.github.com/repos/octocat/Hello-World/releases?per_page=100&page=2>; rel=\"next\"");
                return Task.FromResult(firstPage);
            }

            return Task.FromResult(JsonResponse(CreateReleasePayload(id: 42, tagName: "v1.2.3")));
        });

        var result = await RetrieveAsync(handler);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(2, requestedUris.Count);
        Assert.EndsWith("page=2", requestedUris[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReleasesAsync_RejectsUntrustedPaginationLinks()
    {
        var response = JsonResponse(CreateReleasePayload());
        response.Headers.TryAddWithoutValidation(
            "Link",
            "<https://attacker.example/releases?page=2>; rel=\"next\"");
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await RetrieveAsync(handler);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.MalformedResponse, result.Error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    private static async Task<UpdateResult<IReadOnlyList<ReleaseInfo>>> RetrieveAsync(
        HttpMessageHandler handler,
        UpdateClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var source = new GitHubReleaseSource(httpClient, options ?? CreateOptions());
        return await source.GetReleasesAsync(cancellationToken);
    }

    private static StubHttpMessageHandler RespondWithJson(params object[] releases) =>
        new((_, _) => Task.FromResult(JsonResponse(releases)));

    private static HttpResponseMessage JsonResponse(params object[] releases) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(releases),
                Encoding.UTF8,
                "application/json"),
        };

    private static object CreateReleasePayload(
        long id = 42,
        string tagName = "v1.2.3",
        bool isPrerelease = false,
        bool isDraft = false,
        object[]? assets = null) =>
        new
        {
            id,
            tag_name = tagName,
            name = $"UpdateKit {tagName.TrimStart('v')}",
            body = "Release notes",
            html_url = $"https://github.com/octocat/Hello-World/releases/tag/{tagName}",
            published_at = new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero),
            prerelease = isPrerelease,
            draft = isDraft,
            assets = assets ?? [CreateAssetPayload()],
        };

    private static object CreateAssetPayload(long size = 1024) =>
        new
        {
            name = "UpdateKit.zip",
            browser_download_url =
                "https://github.com/octocat/Hello-World/releases/download/v1.2.3/UpdateKit.zip",
            size,
            content_type = "application/zip",
        };

    private static UpdateClientOptions CreateOptions(
        string? accessToken = null,
        bool includePrereleases = false,
        TimeSpan? requestTimeout = null) =>
        new()
        {
            RepositoryOwner = "octocat",
            RepositoryName = "Hello-World",
            AccessToken = accessToken,
            IncludePrereleases = includePrereleases,
            RequestTimeout = requestTimeout ?? UpdateClientOptions.DefaultRequestTimeout,
        };
}
