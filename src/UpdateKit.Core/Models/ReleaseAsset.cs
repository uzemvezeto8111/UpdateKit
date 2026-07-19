using UpdateKit.Internal;

namespace UpdateKit;

public sealed record ReleaseAsset
{
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

    public string Name { get; }

    public Uri DownloadUrl { get; }

    public long Size { get; }

    public string? ContentType { get; }
}
