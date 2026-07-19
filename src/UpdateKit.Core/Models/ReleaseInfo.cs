using System.Collections.ObjectModel;
using UpdateKit.Internal;

namespace UpdateKit;

public sealed class ReleaseInfo
{
    public ReleaseInfo(
        long id,
        string tagName,
        string? name,
        string? body,
        Uri htmlUrl,
        DateTimeOffset? publishedAt,
        bool isPrerelease,
        bool isDraft,
        IEnumerable<ReleaseAsset> assets)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Release ID must be positive.");
        }

        ArgumentNullException.ThrowIfNull(assets);

        Id = id;
        TagName = Guard.NotWhiteSpace(tagName, nameof(tagName));
        Name = name;
        Body = body;
        HtmlUrl = Guard.AbsoluteUri(htmlUrl, nameof(htmlUrl));
        PublishedAt = publishedAt;
        IsPrerelease = isPrerelease;
        IsDraft = isDraft;
        Assets = new ReadOnlyCollection<ReleaseAsset>(assets.ToArray());
    }

    public long Id { get; }

    public string TagName { get; }

    public string? Name { get; }

    public string? Body { get; }

    public Uri HtmlUrl { get; }

    public DateTimeOffset? PublishedAt { get; }

    public bool IsPrerelease { get; }

    public bool IsDraft { get; }

    public IReadOnlyList<ReleaseAsset> Assets { get; }
}
