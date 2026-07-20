using System.Globalization;

namespace UpdateKit.GitHub;

internal static class GitHubApiEndpoint
{
    public static readonly Uri BaseAddress = new("https://api.github.com/");

    public static bool IsTrusted(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.Equals(uri.Host, BaseAddress.Host, StringComparison.OrdinalIgnoreCase);

    public static bool IsReleaseAsset(
        Uri uri,
        string? expectedOwner = null,
        string? expectedRepository = null,
        long? expectedAssetId = null)
    {
        if (!IsTrusted(uri) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 6 ||
            !string.Equals(segments[0], "repos", StringComparison.Ordinal) ||
            !string.Equals(segments[3], "releases", StringComparison.Ordinal) ||
            !string.Equals(segments[4], "assets", StringComparison.Ordinal) ||
            !long.TryParse(
                segments[5],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var assetId) ||
            assetId <= 0)
        {
            return false;
        }

        return (expectedOwner is null ||
                string.Equals(
                    Uri.UnescapeDataString(segments[1]),
                    expectedOwner,
                    StringComparison.OrdinalIgnoreCase)) &&
            (expectedRepository is null ||
             string.Equals(
                 Uri.UnescapeDataString(segments[2]),
                 expectedRepository,
                 StringComparison.OrdinalIgnoreCase)) &&
            (expectedAssetId is null || assetId == expectedAssetId.Value);
    }
}
