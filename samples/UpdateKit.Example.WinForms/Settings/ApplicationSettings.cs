using UpdateKit.Example.WinForms.Configuration;
using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms.Settings;

internal sealed record ApplicationSettings
{
    public const int CurrentVersion = 1;
    public const int MaximumAllowedRetries = 10;
    public const int MaximumRetryDelayMilliseconds = 300_000;

    public int Version { get; init; } = CurrentVersion;
    public ApplicationTheme Theme { get; init; } = ApplicationTheme.System;
    public bool IncludePrereleaseVersions { get; init; }
    public bool AutomaticallyCheckForUpdates { get; init; }
    public bool ConfirmBeforeDownload { get; init; } = true;
    public bool OpenDestinationFolderAfterDownload { get; init; }
    public bool RememberRepository { get; init; } = true;
    public bool RememberAssetSelection { get; init; } = true;
    public bool RememberDestinationDirectory { get; init; } = true;
    public string? RepositoryOwner { get; init; }
    public string? RepositoryName { get; init; }
    public SampleAssetSelectionMode AssetSelectionMode { get; init; } = SampleAssetSelectionMode.Extension;
    public string AssetSelectionValue { get; init; } = ".nupkg";
    public string? LastDestinationDirectory { get; init; }
    public string DefaultDownloadDirectory { get; init; } = string.Empty;
    public int MaximumRetryCount { get; init; }
    public int RetryDelayMilliseconds { get; init; } = 1_000;

    public static ApplicationSettings CreateDefaults(string defaultDownloadDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultDownloadDirectory);
        return new ApplicationSettings
        {
            DefaultDownloadDirectory = Path.GetFullPath(defaultDownloadDirectory),
        };
    }

    public ApplicationSettings Normalize(ApplicationSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        return this with
        {
            Version = CurrentVersion,
            Theme = Enum.IsDefined(Theme) ? Theme : defaults.Theme,
            AssetSelectionMode = Enum.IsDefined(AssetSelectionMode)
                ? AssetSelectionMode
                : defaults.AssetSelectionMode,
            AssetSelectionValue = string.IsNullOrWhiteSpace(AssetSelectionValue)
                ? defaults.AssetSelectionValue
                : AssetSelectionValue.Trim(),
            DefaultDownloadDirectory = !IsExistingAbsoluteDirectory(DefaultDownloadDirectory)
                ? defaults.DefaultDownloadDirectory
                : DefaultDownloadDirectory.Trim(),
            MaximumRetryCount = MaximumRetryCount is >= 0 and <= MaximumAllowedRetries
                ? MaximumRetryCount
                : defaults.MaximumRetryCount,
            RetryDelayMilliseconds = RetryDelayMilliseconds is >= 0 and <= MaximumRetryDelayMilliseconds
                ? RetryDelayMilliseconds
                : defaults.RetryDelayMilliseconds,
            RepositoryOwner = NormalizeOptional(RepositoryOwner),
            RepositoryName = NormalizeOptional(RepositoryName),
            LastDestinationDirectory = IsExistingAbsoluteDirectory(LastDestinationDirectory)
                ? LastDestinationDirectory!.Trim()
                : null,
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsExistingAbsoluteDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            return false;
        }

        try
        {
            return Directory.Exists(Path.GetFullPath(value));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
