using UpdateKit.Internal;

namespace UpdateKit;

/// <summary>Describes a downloadable file attached to a GitHub release.</summary>
public sealed record ReleaseAsset
{
    /// <summary>Creates release-asset metadata.</summary>
    public ReleaseAsset(string name, Uri downloadUrl, long size, string? contentType = null)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Asset size cannot be negative.");
        }

        Name = Guard.NotWhiteSpace(name, nameof(name));
        DownloadUrl = Guard.AbsoluteUri(downloadUrl, nameof(downloadUrl));
        Size = size;
        ContentType = contentType;
    }

    /// <summary>Gets the asset filename.</summary>
    public string Name { get; }

    /// <summary>Gets the absolute asset-download URL.</summary>
    public Uri DownloadUrl { get; }

    /// <summary>Gets the GitHub-reported asset size in bytes.</summary>
    public long Size { get; }

    /// <summary>Gets the optional media type reported by GitHub.</summary>
    public string? ContentType { get; }
}
