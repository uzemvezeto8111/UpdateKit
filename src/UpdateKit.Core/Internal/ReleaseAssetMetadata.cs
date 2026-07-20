using System.Runtime.CompilerServices;

namespace UpdateKit.Internal;

internal static class ReleaseAssetMetadata
{
    private static readonly ConditionalWeakTable<ReleaseAsset, GitHubAssetMetadata> GitHubAssets = new();

    public static void SetGitHubApiDownloadUri(ReleaseAsset asset, Uri apiDownloadUri)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(apiDownloadUri);
        GitHubAssets.Add(asset, new GitHubAssetMetadata(apiDownloadUri));
    }

    public static bool TryGetGitHubApiDownloadUri(ReleaseAsset asset, out Uri apiDownloadUri)
    {
        if (GitHubAssets.TryGetValue(asset, out var metadata))
        {
            apiDownloadUri = metadata.ApiDownloadUri;
            return true;
        }

        apiDownloadUri = null!;
        return false;
    }

    private sealed record GitHubAssetMetadata(Uri ApiDownloadUri);
}
