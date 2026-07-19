namespace UpdateKit.Core.Tests.Models;

public sealed class ReleaseAssetTests
{
    [Fact]
    public void Constructor_PreservesValidatedValues()
    {
        var uri = new Uri("https://example.test/UpdateKit.zip");

        var asset = new ReleaseAsset("UpdateKit.zip", uri, 2048, "application/zip");

        Assert.Equal("UpdateKit.zip", asset.Name);
        Assert.Equal(uri, asset.DownloadUrl);
        Assert.Equal(2048, asset.Size);
        Assert.Equal("application/zip", asset.ContentType);
    }

    [Fact]
    public void EquivalentAssets_HaveValueEquality()
    {
        var first = TestData.Asset();
        var second = TestData.Asset();

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new ReleaseAsset(name!, new Uri("https://example.test/asset"), 1));
    }

    [Fact]
    public void Constructor_RejectsRelativeDownloadUrl()
    {
        Assert.Throws<ArgumentException>(
            () => new ReleaseAsset("asset.zip", new Uri("asset.zip", UriKind.Relative), 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReleaseAsset("asset.zip", new Uri("https://example.test/asset.zip"), -1));
    }
}
