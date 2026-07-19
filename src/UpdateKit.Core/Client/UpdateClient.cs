using UpdateKit.GitHub;

namespace UpdateKit;

/// <summary>
/// Coordinates GitHub release checks, version comparison, asset selection, downloads, and checksum verification.
/// </summary>
public sealed class UpdateClient
{
    private readonly IGitHubReleaseSource _releaseSource;
    private readonly AssetDownloader _assetDownloader;
    private readonly Sha256Verifier _sha256Verifier;

    /// <summary>
    /// Creates an update client that uses, but does not own or dispose, the supplied HTTP client.
    /// </summary>
    public UpdateClient(HttpClient httpClient, UpdateClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _releaseSource = new GitHubReleaseSource(httpClient, options);
        _assetDownloader = new AssetDownloader(httpClient);
        _sha256Verifier = new Sha256Verifier(httpClient);
    }

    internal UpdateClient(
        IGitHubReleaseSource releaseSource,
        AssetDownloader assetDownloader,
        Sha256Verifier sha256Verifier)
    {
        _releaseSource = releaseSource ?? throw new ArgumentNullException(nameof(releaseSource));
        _assetDownloader = assetDownloader ?? throw new ArgumentNullException(nameof(assetDownloader));
        _sha256Verifier = sha256Verifier ?? throw new ArgumentNullException(nameof(sha256Verifier));
    }

    /// <summary>
    /// Retrieves eligible releases and compares the newest release tag with the caller's current version.
    /// </summary>
    /// <remarks>Caller-requested cancellation is propagated as <see cref="OperationCanceledException"/>.</remarks>
    public async Task<UpdateResult<UpdateCheckResult>> CheckForUpdateAsync(
        string? currentVersion,
        CancellationToken cancellationToken = default)
    {
        var currentVersionResult = SemanticVersion.ParseTag(currentVersion);
        if (!currentVersionResult.IsSuccess)
        {
            return UpdateResult<UpdateCheckResult>.Failure(currentVersionResult.Error);
        }

        var releasesResult = await _releaseSource
            .GetReleasesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!releasesResult.IsSuccess)
        {
            return UpdateResult<UpdateCheckResult>.Failure(releasesResult.Error);
        }

        ReleaseInfo? latestRelease = null;
        SemanticVersion? latestVersion = null;

        foreach (var release in releasesResult.Value)
        {
            var releaseVersionResult = SemanticVersion.ParseTag(release.TagName);
            if (!releaseVersionResult.IsSuccess)
            {
                return UpdateResult<UpdateCheckResult>.Failure(
                    new UpdateError(
                        UpdateErrorCode.InvalidVersion,
                        $"An eligible release has an invalid version tag. {releaseVersionResult.Error.Message}",
                        releaseVersionResult.Error.Exception));
            }

            if (latestVersion is null ||
                releaseVersionResult.Value.CompareTo(latestVersion) > 0)
            {
                latestRelease = release;
                latestVersion = releaseVersionResult.Value;
            }
        }

        if (latestRelease is null || latestVersion is null)
        {
            return UpdateResult<UpdateCheckResult>.Failure(
                new UpdateError(
                    UpdateErrorCode.NoReleaseFound,
                    "The repository has no eligible published releases."));
        }

        var checkResult = latestVersion.CompareTo(currentVersionResult.Value) > 0
            ? UpdateCheckResult.UpdateAvailable(currentVersion!, latestRelease)
            : UpdateCheckResult.NoUpdate(currentVersion!, latestRelease);

        return UpdateResult<UpdateCheckResult>.Success(checkResult);
    }

    /// <summary>Selects the first asset whose name matches exactly using ordinal comparison.</summary>
    public UpdateResult<ReleaseAsset> SelectAssetByExactName(
        ReleaseInfo release,
        string? assetName) =>
        AssetSelector.ByExactName(release, assetName);

    /// <summary>Selects the first asset with the supplied case-insensitive file extension.</summary>
    public UpdateResult<ReleaseAsset> SelectAssetByExtension(
        ReleaseInfo release,
        string? extension) =>
        AssetSelector.ByExtension(release, extension);

    /// <summary>Selects the first asset accepted by a caller-provided predicate.</summary>
    public UpdateResult<ReleaseAsset> SelectAssetByPredicate(
        ReleaseInfo release,
        Func<ReleaseAsset, bool>? predicate) =>
        AssetSelector.ByPredicate(release, predicate);

    /// <summary>Streams an asset to an absolute destination path with optional progress and cancellation.</summary>
    public Task<UpdateResult<DownloadResult>> DownloadAsync(
        ReleaseAsset asset,
        string? destinationFilePath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _assetDownloader.DownloadAsync(
            asset,
            destinationFilePath,
            progress,
            cancellationToken);

    /// <summary>Downloads an asset and verifies it against a directly supplied SHA-256 checksum.</summary>
    public async Task<UpdateResult<DownloadResult>> DownloadAndVerifyAsync(
        ReleaseAsset asset,
        string? destinationFilePath,
        string? expectedSha256,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadResult = await DownloadAsync(
                asset,
                destinationFilePath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (!downloadResult.IsSuccess)
        {
            return downloadResult;
        }

        return await _sha256Verifier
            .VerifyAsync(downloadResult.Value, expectedSha256, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Downloads an asset and verifies it using an entry from another release asset.</summary>
    public async Task<UpdateResult<DownloadResult>> DownloadAndVerifyFromChecksumFileAsync(
        ReleaseAsset asset,
        string? destinationFilePath,
        ReleaseAsset checksumAsset,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checksumAsset);

        var downloadResult = await DownloadAsync(
                asset,
                destinationFilePath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (!downloadResult.IsSuccess)
        {
            return downloadResult;
        }

        return await _sha256Verifier
            .VerifyFromChecksumFileAsync(
                downloadResult.Value,
                checksumAsset,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
