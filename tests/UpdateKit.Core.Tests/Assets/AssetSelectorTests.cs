namespace UpdateKit.Core.Tests.Assets;

public sealed class AssetSelectorTests
{
    [Fact]
    public void ByExactName_ReturnsExactCaseSensitiveMatch()
    {
        var expected = CreateAsset("UpdateKit-win-x64.zip");
        var release = CreateRelease(
            CreateAsset("UpdateKit-linux-x64.tar.gz"),
            expected,
            CreateAsset("UpdateKit-win-x64.msi"));

        var result = AssetSelector.ByExactName(release, "UpdateKit-win-x64.zip");

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public void ByExactName_TreatsCaseDifferencesAsNoMatch()
    {
        var release = CreateRelease(CreateAsset("UpdateKit.zip"));

        var result = AssetSelector.ByExactName(release, "updatekit.zip");

        AssertAssetNotFound(result);
    }

    [Fact]
    public void ByExactName_AllowsOrdinarySpacesInAssetName()
    {
        var expected = CreateAsset("Update Kit.zip");
        var release = CreateRelease(expected);

        var result = AssetSelector.ByExactName(release, "Update Kit.zip");

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public void ByExactName_ReturnsFirstMatchInReleaseOrder()
    {
        var first = CreateAsset("UpdateKit.zip", size: 100);
        var second = CreateAsset("UpdateKit.zip", size: 200);
        var release = CreateRelease(first, second);

        var result = AssetSelector.ByExactName(release, "UpdateKit.zip");

        Assert.True(result.IsSuccess);
        Assert.Same(first, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("folder/UpdateKit.zip")]
    [InlineData("folder\\UpdateKit.zip")]
    [InlineData("UpdateKit\n.zip")]
    public void ByExactName_RejectsInvalidCriteria(string? assetName)
    {
        var result = AssetSelector.ByExactName(
            CreateRelease(CreateAsset("UpdateKit.zip")),
            assetName);

        AssertInvalidCriteria(result);
    }

    [Theory]
    [InlineData("zip")]
    [InlineData(".zip")]
    [InlineData("ZIP")]
    [InlineData(".ZIP")]
    public void ByExtension_NormalizesOptionalDotAndMatchesCaseInsensitively(string extension)
    {
        var expected = CreateAsset("UpdateKit.ZiP");
        var release = CreateRelease(CreateAsset("UpdateKit.exe"), expected);

        var result = AssetSelector.ByExtension(release, extension);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Theory]
    [InlineData("tar.gz")]
    [InlineData(".tar.gz")]
    [InlineData("TAR.GZ")]
    public void ByExtension_SupportsMultipartSuffixes(string extension)
    {
        var expected = CreateAsset("UpdateKit-linux-x64.tar.gz");
        var release = CreateRelease(CreateAsset("UpdateKit.zip"), expected);

        var result = AssetSelector.ByExtension(release, extension);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Theory]
    [InlineData(".zip", "Product.zip")]
    [InlineData("exe", "Product.exe")]
    [InlineData(".msi", "Product.msi")]
    [InlineData("nupkg", "Product.nupkg")]
    [InlineData(".7z", "Product.7z")]
    [InlineData("tar.gz", "Product.tar.gz")]
    public void ByExtension_SelectsArbitraryFileTypesWithoutSpecialCases(
        string extension,
        string assetName)
    {
        var expected = CreateAsset(assetName);
        var release = CreateRelease(CreateAsset("unrelated.txt"), expected);

        var result = AssetSelector.ByExtension(release, extension);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(assetName, result.Value.Name);
    }

    [Fact]
    public void ByExtension_ReturnsFirstMatchInReleaseOrder()
    {
        var first = CreateAsset("UpdateKit-win-x64.zip");
        var second = CreateAsset("UpdateKit-win-arm64.zip");
        var release = CreateRelease(first, second);

        var result = AssetSelector.ByExtension(release, ".zip");

        Assert.True(result.IsSuccess);
        Assert.Same(first, result.Value);
    }

    [Fact]
    public void ByExtension_ReturnsAssetNotFoundWhenNothingMatches()
    {
        var release = CreateRelease(CreateAsset("UpdateKit.zip"));

        var result = AssetSelector.ByExtension(release, "msi");

        AssertAssetNotFound(result);
        Assert.Contains(".msi", result.Error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..zip")]
    [InlineData("tar..gz")]
    [InlineData("zip.")]
    [InlineData("zip/file")]
    [InlineData("zip\\file")]
    [InlineData("*.zip")]
    [InlineData("zip?")]
    [InlineData("zip:")]
    [InlineData("z ip")]
    [InlineData("zip\r\n")]
    public void ByExtension_RejectsInvalidCriteria(string? extension)
    {
        var result = AssetSelector.ByExtension(
            CreateRelease(CreateAsset("UpdateKit.zip")),
            extension);

        AssertInvalidCriteria(result);
    }

    [Fact]
    public void ByPredicate_ReturnsFirstMatchingAsset()
    {
        var first = CreateAsset("UpdateKit-small.zip", size: 100);
        var expected = CreateAsset("UpdateKit-medium.zip", size: 500);
        var laterMatch = CreateAsset("UpdateKit-large.zip", size: 1000);
        var release = CreateRelease(first, expected, laterMatch);
        var invocationCount = 0;

        var result = AssetSelector.ByPredicate(
            release,
            asset =>
            {
                invocationCount++;
                return asset.Size >= 500;
            });

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(2, invocationCount);
    }

    [Fact]
    public void ByPredicate_CanMatchAnyReleaseAssetProperty()
    {
        var expected = CreateAsset("UpdateKit.msi", contentType: "application/x-msi");
        var release = CreateRelease(CreateAsset("UpdateKit.zip"), expected);

        var result = AssetSelector.ByPredicate(
            release,
            asset => asset.ContentType == "application/x-msi");

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public void ByPredicate_ReturnsAssetNotFoundWhenNothingMatches()
    {
        var release = CreateRelease(CreateAsset("UpdateKit.zip"));

        var result = AssetSelector.ByPredicate(release, asset => asset.Size > 10_000);

        AssertAssetNotFound(result);
    }

    [Fact]
    public void ByPredicate_RejectsNullPredicate()
    {
        var result = AssetSelector.ByPredicate(
            CreateRelease(CreateAsset("UpdateKit.zip")),
            null);

        AssertInvalidCriteria(result);
    }

    [Fact]
    public void ByPredicate_PropagatesCallerException()
    {
        var expected = new InvalidOperationException("Predicate failed.");
        var release = CreateRelease(CreateAsset("UpdateKit.zip"));

        var actual = Assert.Throws<InvalidOperationException>(
            () => AssetSelector.ByPredicate(release, _ => throw expected));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void EverySelectionMode_ReturnsAssetNotFoundForEmptyRelease()
    {
        var release = CreateRelease();

        AssertAssetNotFound(AssetSelector.ByExactName(release, "UpdateKit.zip"));
        AssertAssetNotFound(AssetSelector.ByExtension(release, ".zip"));
        AssertAssetNotFound(AssetSelector.ByPredicate(release, _ => true));
    }

    [Fact]
    public void EverySelectionMode_RejectsNullRelease()
    {
        Assert.Throws<ArgumentNullException>(
            () => AssetSelector.ByExactName(null!, "UpdateKit.zip"));
        Assert.Throws<ArgumentNullException>(
            () => AssetSelector.ByExtension(null!, ".zip"));
        Assert.Throws<ArgumentNullException>(
            () => AssetSelector.ByPredicate(null!, _ => true));
    }

    private static void AssertAssetNotFound(UpdateResult<ReleaseAsset> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.AssetNotFound, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static void AssertInvalidCriteria(UpdateResult<ReleaseAsset> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.InvalidConfiguration, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static ReleaseInfo CreateRelease(params ReleaseAsset[] assets) =>
        new(
            id: 42,
            tagName: "v1.2.3",
            name: "UpdateKit 1.2.3",
            body: null,
            htmlUrl: new Uri("https://example.test/releases/v1.2.3"),
            publishedAt: DateTimeOffset.UnixEpoch,
            isPrerelease: false,
            isDraft: false,
            assets: assets);

    private static ReleaseAsset CreateAsset(
        string name,
        long size = 1024,
        string contentType = "application/octet-stream") =>
        new(
            name,
            new Uri($"https://example.test/assets/{Uri.EscapeDataString(name)}"),
            size,
            contentType);
}
