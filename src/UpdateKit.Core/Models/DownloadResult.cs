namespace UpdateKit;

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

    public ReleaseAsset Asset { get; }

    public string FilePath { get; }

    public long BytesDownloaded { get; }
}
