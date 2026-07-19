namespace UpdateKit.WinForms;

public sealed class UpdateDialogOptions
{
    public UpdateDialogOptions(
        UpdateClient client,
        string currentVersion,
        string destinationFilePath,
        Func<ReleaseInfo, UpdateResult<ReleaseAsset>> assetSelector)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
        DestinationFilePath = destinationFilePath ??
            throw new ArgumentNullException(nameof(destinationFilePath));
        AssetSelector = assetSelector ?? throw new ArgumentNullException(nameof(assetSelector));
    }

    public UpdateClient Client { get; }

    public string CurrentVersion { get; }

    public string DestinationFilePath { get; }

    public Func<ReleaseInfo, UpdateResult<ReleaseAsset>> AssetSelector { get; }

    public string DialogTitle { get; init; } = "Software Update";

    public bool CheckForUpdateOnShown { get; init; } = true;

    public string? ExpectedSha256 { get; init; }

    public Func<ReleaseInfo, UpdateResult<ReleaseAsset>>? ChecksumAssetSelector { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DialogTitle))
        {
            throw new ArgumentException("A dialog title is required.", nameof(DialogTitle));
        }

        if (ExpectedSha256 is not null && ChecksumAssetSelector is not null)
        {
            throw new ArgumentException(
                "Configure either a direct SHA-256 checksum or a checksum-file asset selector, not both.");
        }
    }
}
