namespace UpdateKit.GitHub;

internal interface IGitHubReleaseSource
{
    Task<UpdateResult<IReadOnlyList<ReleaseInfo>>> GetReleasesAsync(
        CancellationToken cancellationToken = default);
}
