using UpdateKit.Example.WinForms.Configuration;

namespace UpdateKit.Example.WinForms.Settings;

internal sealed record MainFormSettingsState(
    string RepositoryOwner,
    string RepositoryName,
    bool IncludePrereleases,
    SampleAssetSelectionMode AssetSelectionMode,
    string AssetSelectionValue,
    string DestinationFilePath);

internal static class MainFormSettingsMapper
{
    public static MainFormSettingsState ToFormState(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = settings.RememberDestinationDirectory &&
            Directory.Exists(settings.LastDestinationDirectory)
                ? settings.LastDestinationDirectory!
                : settings.DefaultDownloadDirectory;

        return new(
            settings.RememberRepository ? settings.RepositoryOwner ?? string.Empty : string.Empty,
            settings.RememberRepository ? settings.RepositoryName ?? string.Empty : string.Empty,
            settings.IncludePrereleaseVersions,
            settings.RememberAssetSelection ? settings.AssetSelectionMode : SampleAssetSelectionMode.Extension,
            settings.RememberAssetSelection ? settings.AssetSelectionValue : ".nupkg",
            directory);
    }

    public static ApplicationSettings Capture(
        ApplicationSettings settings,
        string repositoryOwner,
        string repositoryName,
        SampleAssetSelectionMode assetMode,
        string assetValue,
        string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? destinationDirectory = null;
        try
        {
            var fullPath = Path.GetFullPath(destinationFilePath);
            destinationDirectory = Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
        }

        return settings with
        {
            RepositoryOwner = settings.RememberRepository ? NullIfWhiteSpace(repositoryOwner) : null,
            RepositoryName = settings.RememberRepository ? NullIfWhiteSpace(repositoryName) : null,
            AssetSelectionMode = settings.RememberAssetSelection ? assetMode : SampleAssetSelectionMode.Extension,
            AssetSelectionValue = settings.RememberAssetSelection && !string.IsNullOrWhiteSpace(assetValue)
                ? assetValue.Trim()
                : ".nupkg",
            LastDestinationDirectory = settings.RememberDestinationDirectory ? destinationDirectory : null,
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
