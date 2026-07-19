namespace UpdateKit;

/// <summary>Represents a point-in-time streaming download measurement.</summary>
public sealed record DownloadProgress
{
    /// <summary>Creates a progress measurement with an optional server-reported total length.</summary>
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

    /// <summary>Gets the number of bytes written so far.</summary>
    public long BytesDownloaded { get; }

    /// <summary>Gets the server-reported total byte count, when known.</summary>
    public long? TotalBytes { get; }

    /// <summary>Gets a percentage clamped to 100, or <see langword="null"/> when a total is unavailable.</summary>
    public double? Percentage => TotalBytes is > 0
        ? Math.Min(100d, BytesDownloaded * 100d / TotalBytes.Value)
        : null;
}
