namespace UpdateKit;

/// <summary>Describes a successfully committed asset download.</summary>
public sealed record DownloadResult
{
    internal DownloadResult(
        ReleaseAsset asset,
        string filePath,
        long bytesDownloaded)
    {
        Asset = asset;
        FilePath = filePath;
        BytesDownloaded = bytesDownloaded;
    }

    /// <summary>Gets the downloaded release asset.</summary>
    public ReleaseAsset Asset { get; }

    /// <summary>Gets the resolved absolute destination file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets the number of downloaded bytes.</summary>
    public long BytesDownloaded { get; }
}
