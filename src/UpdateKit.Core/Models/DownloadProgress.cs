namespace UpdateKit;

public sealed record DownloadProgress
{
    public DownloadProgress(long bytesDownloaded, long? totalBytes)
    {
        if (bytesDownloaded < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytesDownloaded),
                bytesDownloaded,
                "Downloaded bytes cannot be negative.");
        }

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBytes),
                totalBytes,
                "Total bytes cannot be negative.");
        }

        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
    }

    public long BytesDownloaded { get; }

    public long? TotalBytes { get; }

    public double? Percentage => TotalBytes is > 0
        ? Math.Min(100d, BytesDownloaded * 100d / TotalBytes.Value)
        : null;
}
