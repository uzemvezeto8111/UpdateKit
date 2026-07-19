using System.Collections.ObjectModel;
using UpdateKit.Internal;

namespace UpdateKit;

/// <summary>Describes an eligible GitHub release and its immutable asset collection.</summary>
public sealed class ReleaseInfo
{
    /// <summary>Creates release metadata.</summary>
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

    /// <summary>Gets the positive GitHub release identifier.</summary>
    public long Id { get; }

    /// <summary>Gets the release tag used for Semantic Versioning comparison.</summary>
    public string TagName { get; }

    /// <summary>Gets the optional display name.</summary>
    public string? Name { get; }

    /// <summary>Gets the optional release-notes body.</summary>
    public string? Body { get; }

    /// <summary>Gets the absolute GitHub web URL for the release.</summary>
    public Uri HtmlUrl { get; }

    /// <summary>Gets the publication timestamp when available.</summary>
    public DateTimeOffset? PublishedAt { get; }

    /// <summary>Gets whether GitHub marks the release as a prerelease.</summary>
    public bool IsPrerelease { get; }

    /// <summary>Gets whether GitHub marks the release as a draft.</summary>
    public bool IsDraft { get; }

    /// <summary>Gets an immutable snapshot of the release assets.</summary>
    public IReadOnlyList<ReleaseAsset> Assets { get; }
}
