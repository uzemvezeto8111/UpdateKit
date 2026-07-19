namespace UpdateKit.Core.Tests.Models;

public sealed class ReleaseInfoTests
{
    [Fact]
    public void Constructor_CopiesAndProtectsAssetCollection()
    {
        var asset = TestData.Asset();
        var source = new List<ReleaseAsset> { asset };

        var release = new ReleaseInfo(
            42,
            "v1.2.3",
            "Release",
            null,
            new Uri("https://example.test/releases/42"),
            null,
            isPrerelease: true,
            isDraft: true,
            source);
        source.Clear();

        Assert.Same(asset, Assert.Single(release.Assets));
        Assert.True(release.IsPrerelease);
        Assert.True(release.IsDraft);

        var mutableView = Assert.IsAssignableFrom<IList<ReleaseAsset>>(release.Assets);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(TestData.Asset("other.zip")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveId(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRelease(id: id));
    }

    [Fact]
    public void Constructor_RejectsMissingTag()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateRelease(tagName: " "));
    }

    [Fact]
    public void Constructor_RejectsRelativeHtmlUrl()
    {
        Assert.Throws<ArgumentException>(
            () => CreateRelease(htmlUrl: new Uri("releases/42", UriKind.Relative)));
    }

    [Fact]
    public void Constructor_RejectsNullAssets()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ReleaseInfo(
                42,
                "v1.2.3",
                "Release",
                null,
                new Uri("https://example.test/releases/42"),
                DateTimeOffset.UtcNow,
                false,
                false,
                null!));
    }

    private static ReleaseInfo CreateRelease(
        long id = 42,
        string tagName = "v1.2.3",
        Uri? htmlUrl = null,
        IEnumerable<ReleaseAsset>? assets = null) =>
        new(
            id,
            tagName,
            "Release",
            null,
            htmlUrl ?? new Uri("https://example.test/releases/42"),
            DateTimeOffset.UtcNow,
            false,
            false,
            assets ?? [TestData.Asset()]);
}
