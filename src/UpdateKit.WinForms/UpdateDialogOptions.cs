namespace UpdateKit.WinForms;

/// <summary>Configures one display of an <see cref="UpdateDialog"/>.</summary>
public sealed class UpdateDialogOptions
{
    /// <summary>Creates options for a client, current version, destination, and asset-selection strategy.</summary>
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

    /// <summary>Gets the update client borrowed by the dialog.</summary>
    public UpdateClient Client { get; }

    /// <summary>Gets the caller's current Semantic Versioning tag.</summary>
    public string CurrentVersion { get; }

    /// <summary>Gets the absolute destination file path.</summary>
    public string DestinationFilePath { get; }

    /// <summary>Gets the host-provided primary asset selector.</summary>
    public Func<ReleaseInfo, UpdateResult<ReleaseAsset>> AssetSelector { get; }

    /// <summary>Gets the native window title.</summary>
    public string DialogTitle { get; init; } = "Software Update";

    /// <summary>Gets whether the first display automatically starts an update check.</summary>
    public bool CheckForUpdateOnShown { get; init; } = true;

    /// <summary>Gets an optional direct expected SHA-256 checksum.</summary>
    public string? ExpectedSha256 { get; init; }

    /// <summary>Gets an optional release checksum-file asset selector.</summary>
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
