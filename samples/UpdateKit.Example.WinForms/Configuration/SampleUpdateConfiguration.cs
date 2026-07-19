using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms.Configuration;

internal sealed record SampleUpdateConfiguration(
    string RepositoryOwner,
    string RepositoryName,
    string? AccessToken,
    string CurrentVersion,
    bool IncludePrereleases,
    SampleAssetSelectionMode AssetSelectionMode,
    string AssetSelectionValue,
    string DestinationFilePath,
    SampleVerificationMode VerificationMode,
    string? VerificationValue)
{
    public UpdateClientOptions CreateClientOptions() =>
        new()
        {
            RepositoryOwner = RepositoryOwner,
            RepositoryName = RepositoryName,
            AccessToken = AccessToken,
            IncludePrereleases = IncludePrereleases,
            UserAgent = "UpdateKit.Example.WinForms",
        };

    public UpdateDialogOptions CreateDialogOptions(UpdateClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        Func<ReleaseInfo, UpdateResult<ReleaseAsset>> assetSelector =
            AssetSelectionMode == SampleAssetSelectionMode.ExactName
                ? release => client.SelectAssetByExactName(release, AssetSelectionValue)
                : release => client.SelectAssetByExtension(release, AssetSelectionValue);

        return new UpdateDialogOptions(
            client,
            CurrentVersion,
            DestinationFilePath,
            assetSelector)
        {
            DialogTitle = "UpdateKit Example — Software Update",
            ExpectedSha256 = VerificationMode == SampleVerificationMode.DirectSha256
                ? VerificationValue
                : null,
            ChecksumAssetSelector = VerificationMode == SampleVerificationMode.ChecksumFile
                ? release => client.SelectAssetByExactName(release, VerificationValue)
                : null,
        };
    }
}
