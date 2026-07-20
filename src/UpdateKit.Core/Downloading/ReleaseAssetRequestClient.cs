using System.Net;
using System.Net.Http.Headers;
using UpdateKit.GitHub;
using UpdateKit.Internal;

namespace UpdateKit;

internal sealed class ReleaseAssetRequestClient
{
    private const int MaximumRedirects = 10;

    private static readonly MediaTypeWithQualityHeaderValue BinaryMediaType =
        new("application/octet-stream");

    private readonly HttpClient _httpClient;
    private readonly string? _accessToken;
    private readonly string _userAgent;

    public ReleaseAssetRequestClient(
        HttpClient httpClient,
        string? accessToken = null,
        string userAgent = UpdateClientOptions.DefaultUserAgent)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessToken = accessToken;
        _userAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
    }

    public bool TryCreatePlan(
        ReleaseAsset asset,
        out ReleaseAssetRequestPlan plan,
        out string validationMessage)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_accessToken is not null &&
            ReleaseAssetMetadata.TryGetGitHubApiDownloadUri(asset, out var apiDownloadUri))
        {
            if (!GitHubApiEndpoint.IsReleaseAsset(apiDownloadUri))
            {
                plan = default;
                validationMessage =
                    "The authenticated asset API URL is not a verified GitHub release-asset endpoint.";
                return false;
            }

            plan = new ReleaseAssetRequestPlan(apiDownloadUri, UseAuthentication: true);
            validationMessage = string.Empty;
            return true;
        }

        if (!IsSupportedDownloadUri(asset.DownloadUrl))
        {
            plan = default;
            validationMessage = "The asset download URL must use HTTP or HTTPS.";
            return false;
        }

        plan = new ReleaseAssetRequestPlan(asset.DownloadUrl, UseAuthentication: false);
        validationMessage = string.Empty;
        return true;
    }

    public async Task<ReleaseAssetResponse> SendAsync(
        ReleaseAssetRequestPlan plan,
        CancellationToken cancellationToken)
    {
        var requestUri = plan.RequestUri;

        for (var redirectCount = 0; ; redirectCount++)
        {
            var useAuthentication = plan.UseAuthentication && redirectCount == 0;
            using var request = CreateRequest(requestUri, useAuthentication);
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
            {
                return new ReleaseAssetResponse(response, useAuthentication);
            }

            if (redirectCount >= MaximumRedirects)
            {
                response.Dispose();
                throw new PermanentAssetRequestException(
                    $"The asset request exceeded the limit of {MaximumRedirects} redirects.");
            }

            var redirectUri = ResolveRedirectUri(requestUri, response.Headers.Location);
            response.Dispose();

            if (redirectUri is null ||
                !IsSupportedDownloadUri(redirectUri) ||
                requestUri.Scheme == Uri.UriSchemeHttps &&
                    redirectUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new PermanentAssetRequestException(
                    "The asset response contained an invalid redirect URL.");
            }

            requestUri = redirectUri;
        }
    }

    private HttpRequestMessage CreateRequest(Uri requestUri, bool useAuthentication)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        if (useAuthentication)
        {
            request.Headers.Accept.Add(BinaryMediaType);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            request.Headers.TryAddWithoutValidation(
                "X-GitHub-Api-Version",
                GitHubReleaseSource.ApiVersion);
        }

        return request;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect ||
        (int)statusCode == 308;

    private static Uri? ResolveRedirectUri(Uri requestUri, Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location : new Uri(requestUri, location);
    }

    private static bool IsSupportedDownloadUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class PermanentAssetRequestException : HttpRequestException
{
    public PermanentAssetRequestException(string message)
        : base(message)
    {
    }
}

internal readonly record struct ReleaseAssetRequestPlan(Uri RequestUri, bool UseAuthentication);

internal sealed class ReleaseAssetResponse : IDisposable
{
    public ReleaseAssetResponse(HttpResponseMessage response, bool usedAuthentication)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
        UsedAuthentication = usedAuthentication;
    }

    public HttpResponseMessage Response { get; }

    public bool UsedAuthentication { get; }

    public void Dispose() => Response.Dispose();
}
