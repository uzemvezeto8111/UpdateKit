using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UpdateKit.GitHub;

internal sealed class GitHubReleaseSource : IGitHubReleaseSource
{
    internal const string ApiVersion = "2026-03-10";

    private static readonly Uri ApiBaseAddress = new("https://api.github.com/");
    private static readonly MediaTypeWithQualityHeaderValue GitHubJsonMediaType =
        new("application/vnd.github+json");

    private readonly HttpClient _httpClient;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;
    private readonly string? _accessToken;
    private readonly bool _includePrereleases;
    private readonly string _userAgent;
    private readonly TimeSpan _requestTimeout;

    public GitHubReleaseSource(HttpClient httpClient, UpdateClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _httpClient = httpClient;
        _repositoryOwner = options.RepositoryOwner;
        _repositoryName = options.RepositoryName;
        _accessToken = options.AccessToken;
        _includePrereleases = options.IncludePrereleases;
        _userAgent = options.UserAgent;
        _requestTimeout = options.RequestTimeout;
    }

    public async Task<UpdateResult<IReadOnlyList<ReleaseInfo>>> GetReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var releases = new List<ReleaseInfo>();
        var visitedPages = new HashSet<Uri>();
        var nextPage = CreateInitialRequestUri();

        try
        {
            while (nextPage is not null)
            {
                if (!visitedPages.Add(nextPage))
                {
                    throw new JsonException("The GitHub pagination response contains a cycle.");
                }

                using var request = CreateRequest(nextPage);
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(_requestTimeout);

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await MapHttpErrorAsync(response, timeoutSource.Token).ConfigureAwait(false);
                    return UpdateResult<IReadOnlyList<ReleaseInfo>>.Failure(error);
                }

                await AddEligibleReleasesAsync(
                    response.Content,
                    releases,
                    timeoutSource.Token).ConfigureAwait(false);

                nextPage = GetNextPageUri(response.Headers);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            return Failure(
                UpdateErrorCode.NetworkError,
                $"The GitHub request timed out after {_requestTimeout.TotalSeconds:G} seconds.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            return Failure(
                UpdateErrorCode.NetworkError,
                "The GitHub request failed due to a network error.",
                exception);
        }
        catch (JsonException exception)
        {
            return Failure(
                UpdateErrorCode.MalformedResponse,
                "GitHub returned a malformed release response.",
                exception);
        }
        catch (ArgumentException exception)
        {
            return Failure(
                UpdateErrorCode.MalformedResponse,
                "GitHub returned invalid release data.",
                exception);
        }

        if (releases.Count == 0)
        {
            return Failure(
                UpdateErrorCode.NoReleaseFound,
                "The repository has no eligible published releases.");
        }

        IReadOnlyList<ReleaseInfo> result = new ReadOnlyCollection<ReleaseInfo>(releases);
        return UpdateResult<IReadOnlyList<ReleaseInfo>>.Success(result);
    }

    private Uri CreateInitialRequestUri()
    {
        var owner = Uri.EscapeDataString(_repositoryOwner);
        var repository = Uri.EscapeDataString(_repositoryName);
        return new Uri(ApiBaseAddress, $"repos/{owner}/{repository}/releases?per_page=100");
    }

    private HttpRequestMessage CreateRequest(Uri requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(GitHubJsonMediaType);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", ApiVersion);

        if (_accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return request;
    }

    private async Task AddEligibleReleasesAsync(
        HttpContent content,
        ICollection<ReleaseInfo> destination,
        CancellationToken cancellationToken)
    {
        await using var responseStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var releaseDtos = await JsonSerializer.DeserializeAsync<List<GitHubReleaseDto>>(
            responseStream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (releaseDtos is null)
        {
            throw new JsonException("The release response cannot be null.");
        }

        foreach (var releaseDto in releaseDtos)
        {
            if (releaseDto.IsDraft is null || releaseDto.IsPrerelease is null)
            {
                throw new JsonException("A release is missing its draft or prerelease state.");
            }

            if (releaseDto.IsDraft.Value ||
                (releaseDto.IsPrerelease.Value && !_includePrereleases))
            {
                continue;
            }

            destination.Add(MapRelease(releaseDto));
        }
    }

    private static ReleaseInfo MapRelease(GitHubReleaseDto release)
    {
        if (release.Id is not > 0 ||
            string.IsNullOrWhiteSpace(release.TagName) ||
            !Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var htmlUrl) ||
            release.PublishedAt is null ||
            release.Assets is null)
        {
            throw new JsonException("A release is missing one or more required fields.");
        }

        var assets = release.Assets.Select(MapAsset).ToArray();

        return new ReleaseInfo(
            release.Id.Value,
            release.TagName,
            release.Name,
            release.Body,
            htmlUrl,
            release.PublishedAt,
            release.IsPrerelease!.Value,
            release.IsDraft!.Value,
            assets);
    }

    private static ReleaseAsset MapAsset(GitHubReleaseAssetDto asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Name) ||
            !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUrl) ||
            asset.Size is null or < 0)
        {
            throw new JsonException("A release asset is missing one or more required fields.");
        }

        return new ReleaseAsset(asset.Name, downloadUrl, asset.Size.Value, asset.ContentType);
    }

    private static Uri? GetNextPageUri(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var linkHeaderValues))
        {
            return null;
        }

        foreach (var linkHeaderValue in linkHeaderValues)
        {
            foreach (var link in linkHeaderValue.Split(','))
            {
                var parts = link.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2 ||
                    !parts.Skip(1).Any(IsNextRelation))
                {
                    continue;
                }

                var uriPart = parts[0];
                if (uriPart.Length < 3 ||
                    uriPart[0] != '<' ||
                    uriPart[^1] != '>' ||
                    !Uri.TryCreate(uriPart[1..^1], UriKind.Absolute, out var nextPage) ||
                    !IsTrustedApiUri(nextPage))
                {
                    throw new JsonException("The GitHub pagination link is invalid.");
                }

                return nextPage;
            }
        }

        return null;
    }

    private static bool IsNextRelation(string value)
    {
        const string relationPrefix = "rel=";
        if (!value.StartsWith(relationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relations = value[relationPrefix.Length..].Trim('"').Split(' ');
        return relations.Contains("next", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTrustedApiUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.Equals(uri.Host, ApiBaseAddress.Host, StringComparison.OrdinalIgnoreCase);

    private static async Task<UpdateError> MapHttpErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (IsRateLimited(response) ||
            response.StatusCode == HttpStatusCode.Forbidden &&
            await HasRateLimitErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false))
        {
            return CreateRateLimitError(response.Headers);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new UpdateError(
                UpdateErrorCode.AuthenticationFailed,
                "GitHub rejected the supplied credentials or repository permissions."),
            HttpStatusCode.NotFound => new UpdateError(
                UpdateErrorCode.RepositoryNotFound,
                "The GitHub repository was not found or is not accessible."),
            _ => new UpdateError(
                UpdateErrorCode.NetworkError,
                $"GitHub returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase})."),
        };
    }

    private static async Task<bool> HasRateLimitErrorBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var error = await JsonSerializer.DeserializeAsync<GitHubErrorDto>(
                contentStream,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return error?.Message?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true ||
                error?.DocumentationUrl?.Contains("rate-limit", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return false;
        }

        return response.Headers.RetryAfter is not null ||
            TryGetInt64Header(response.Headers, "X-RateLimit-Remaining", out var remaining) && remaining == 0;
    }

    private static UpdateError CreateRateLimitError(HttpResponseHeaders headers)
    {
        string message;

        if (TryGetInt64Header(headers, "X-RateLimit-Reset", out var resetSeconds) &&
            TryFromUnixTimeSeconds(resetSeconds, out var resetAt))
        {
            message = $"The GitHub API rate limit was exceeded. Requests may resume after {resetAt:O}.";
        }
        else if (headers.RetryAfter?.Delta is { } retryAfter)
        {
            message = $"The GitHub API rate limit was exceeded. Retry after {retryAfter.TotalSeconds:G} seconds.";
        }
        else if (headers.RetryAfter?.Date is { } retryAt)
        {
            message = $"The GitHub API rate limit was exceeded. Requests may resume after {retryAt:O}.";
        }
        else
        {
            message = "The GitHub API rate limit was exceeded.";
        }

        return new UpdateError(UpdateErrorCode.RateLimitExceeded, message);
    }

    private static bool TryGetInt64Header(
        HttpResponseHeaders headers,
        string name,
        out long value)
    {
        value = default;
        return headers.TryGetValues(name, out var values) &&
            long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryFromUnixTimeSeconds(long seconds, out DateTimeOffset value)
    {
        try
        {
            value = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }

    private static UpdateResult<IReadOnlyList<ReleaseInfo>> Failure(
        UpdateErrorCode code,
        string message,
        Exception? exception = null) =>
        UpdateResult<IReadOnlyList<ReleaseInfo>>.Failure(new UpdateError(code, message, exception));
}
