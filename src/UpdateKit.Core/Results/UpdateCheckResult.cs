using UpdateKit.Internal;

namespace UpdateKit;

/// <summary>Describes whether the newest eligible release is newer than the caller's version.</summary>
public sealed class UpdateCheckResult
{
    private UpdateCheckResult(
        string currentVersion,
        ReleaseInfo latestRelease,
        bool isUpdateAvailable)
    {
        CurrentVersion = Guard.NotWhiteSpace(currentVersion, nameof(currentVersion));
        LatestRelease = latestRelease ?? throw new ArgumentNullException(nameof(latestRelease));
        IsUpdateAvailable = isUpdateAvailable;
    }

    /// <summary>Gets the caller-supplied current-version tag.</summary>
    public string CurrentVersion { get; }

    /// <summary>Gets the newest eligible release, including for a no-update result.</summary>
    public ReleaseInfo LatestRelease { get; }

    /// <summary>Gets whether the latest release has greater Semantic Versioning precedence.</summary>
    public bool IsUpdateAvailable { get; }

    /// <summary>Creates an update-available result.</summary>
    public static UpdateCheckResult UpdateAvailable(
        string currentVersion,
        ReleaseInfo latestRelease) =>
        new(currentVersion, latestRelease, true);

    /// <summary>Creates a no-update result.</summary>
    public static UpdateCheckResult NoUpdate(
        string currentVersion,
        ReleaseInfo latestRelease) =>
        new(currentVersion, latestRelease, false);
}
