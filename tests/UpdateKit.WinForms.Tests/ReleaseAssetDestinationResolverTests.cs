using UpdateKit.Desktop.Internal;
using UpdateKit.WinForms.Tests.TestInfrastructure;

namespace UpdateKit.WinForms.Tests;

public sealed class ReleaseAssetDestinationResolverTests
{
    [Theory]
    [InlineData("My Product Setup.exe")]
    [InlineData("MyProduct-linux-x64.tar.gz")]
    [InlineData("MyProduct.7z")]
    public void Resolve_ExistingDirectoryAppendsOriginalAssetFilename(string assetName)
    {
        using var directory = new TemporaryDirectory();

        var result = ReleaseAssetDestinationResolver.Resolve(
            directory.Path,
            CreateAsset(assetName));

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.Combine(directory.Path, assetName), result.Value);
    }

    [Fact]
    public void Resolve_ExplicitFilePathIsPreservedRegardlessOfAssetName()
    {
        using var directory = new TemporaryDirectory();
        var explicitPath = directory.GetPath("renamed download.msi");

        var result = ReleaseAssetDestinationResolver.Resolve(
            explicitPath,
            CreateAsset("original-package.nupkg"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(explicitPath), result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative-file.exe")]
    public void Resolve_RejectsMissingOrRelativeDestination(string? destinationPath)
    {
        var result = ReleaseAssetDestinationResolver.Resolve(
            destinationPath,
            CreateAsset("MyProduct.exe"));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.InvalidConfiguration, result.Error.Code);
    }

    [Fact]
    public void Resolve_RejectsInvalidExplicitFilename()
    {
        using var directory = new TemporaryDirectory();
        var result = ReleaseAssetDestinationResolver.Resolve(
            directory.GetPath("invalid?.msi"),
            CreateAsset("MyProduct.msi"));

        Assert.False(result.IsSuccess);
        Assert.Contains("file name", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RejectsMissingParentDirectoryAndNonexistentDirectoryInput()
    {
        using var directory = new TemporaryDirectory();
        var missingDirectory = directory.GetPath("missing");

        var fileResult = ReleaseAssetDestinationResolver.Resolve(
            Path.Combine(missingDirectory, "MyProduct.exe"),
            CreateAsset("MyProduct.exe"));
        var directoryResult = ReleaseAssetDestinationResolver.Resolve(
            missingDirectory + Path.DirectorySeparatorChar,
            CreateAsset("MyProduct.exe"));

        Assert.False(fileResult.IsSuccess);
        Assert.False(directoryResult.IsSuccess);
        Assert.Contains("does not exist", fileResult.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not exist", directoryResult.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_DirectoryRejectsUnsafeRemoteAssetFilename()
    {
        using var directory = new TemporaryDirectory();
        var result = ReleaseAssetDestinationResolver.Resolve(
            directory.Path,
            CreateAsset("folder/installer.exe"));

        Assert.False(result.IsSuccess);
        Assert.Contains("selected release asset", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ReleaseAsset CreateAsset(string name) =>
        new(name, new Uri("https://downloads.example.test/asset"), 1_024);
}
