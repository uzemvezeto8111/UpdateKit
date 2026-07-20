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
    public static MainFormSettingsState ToFormState(ApplicationSettings settings, string destinationFileName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFileName);
        var directory = settings.RememberDestinationDirectory &&
            Directory.Exists(settings.LastDestinationDirectory)
                ? settings.LastDestinationDirectory!
                : settings.DefaultDownloadDirectory;

        return new(
            settings.RememberRepository ? settings.RepositoryOwner ?? string.Empty : string.Empty,
            settings.RememberRepository ? settings.RepositoryName ?? string.Empty : string.Empty,
            settings.IncludePrereleaseVersions,
            settings.RememberAssetSelection ? settings.AssetSelectionMode : SampleAssetSelectionMode.Extension,
            settings.RememberAssetSelection ? settings.AssetSelectionValue : ".zip",
            Path.Combine(directory, destinationFileName));
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
            destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationFilePath));
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
                : ".zip",
            LastDestinationDirectory = settings.RememberDestinationDirectory ? destinationDirectory : null,
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
