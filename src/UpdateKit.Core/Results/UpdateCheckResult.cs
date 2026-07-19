using UpdateKit.Internal;

namespace UpdateKit;

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

    public string CurrentVersion { get; }

    public ReleaseInfo LatestRelease { get; }

    public bool IsUpdateAvailable { get; }

    public static UpdateCheckResult UpdateAvailable(
        string currentVersion,
        ReleaseInfo latestRelease) =>
        new(currentVersion, latestRelease, true);

    public static UpdateCheckResult NoUpdate(
        string currentVersion,
        ReleaseInfo latestRelease) =>
        new(currentVersion, latestRelease, false);
}
