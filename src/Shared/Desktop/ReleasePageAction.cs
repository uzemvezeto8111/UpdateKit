using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace UpdateKit.Desktop.Internal;

internal interface IReleasePageLauncher
{
    ReleasePageLaunchResult Launch(Uri releasePageUri);
}

internal readonly record struct ReleasePageLaunchResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    Exception? Exception = null)
{
    public static ReleasePageLaunchResult Success() => new(true);

    public static ReleasePageLaunchResult Failure(string message, Exception? exception = null) =>
        new(false, message, exception);
}

internal static class ReleasePageUriValidator
{
    public static bool TryGetSafeReleasePageUri(Uri? candidate, out Uri safeReleasePageUri)
    {
        safeReleasePageUri = null!;

        if (candidate is null ||
            !candidate.IsAbsoluteUri ||
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate.IdnHost, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.IsDefaultPort ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) ||
            !string.IsNullOrEmpty(candidate.Fragment))
        {
            return false;
        }

        var segments = candidate.AbsolutePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var isTagPage = segments.Length >= 5 &&
            string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "tag", StringComparison.OrdinalIgnoreCase);
        var isLatestPage = segments.Length == 4 &&
            string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "latest", StringComparison.OrdinalIgnoreCase);

        if (segments.Length < 4 ||
            string.IsNullOrWhiteSpace(segments[0]) ||
            string.IsNullOrWhiteSpace(segments[1]) ||
            (!isTagPage && !isLatestPage))
        {
            return false;
        }

        safeReleasePageUri = candidate;
        return true;
    }
}

internal sealed class ReleasePageAction
{
    internal const string LaunchFailureMessage =
        "Windows could not open the GitHub release page. Open the repository's Releases page in your browser instead.";

    private readonly IReleasePageLauncher _launcher;

    public ReleasePageAction(IReleasePageLauncher launcher)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public bool IsVisible(Uri? releasePageUri) =>
        ReleasePageUriValidator.TryGetSafeReleasePageUri(releasePageUri, out _);

    public ReleasePageLaunchResult TryOpen(Uri? releasePageUri)
    {
        if (!ReleasePageUriValidator.TryGetSafeReleasePageUri(
                releasePageUri,
                out var safeReleasePageUri))
        {
            return ReleasePageLaunchResult.Failure(
                "The release page address is unavailable or is not a trusted GitHub HTTPS URL.");
        }

        return _launcher.Launch(safeReleasePageUri);
    }
}

internal sealed class ShellReleasePageLauncher : IReleasePageLauncher
{
    public ReleasePageLaunchResult Launch(Uri releasePageUri)
    {
        if (!ReleasePageUriValidator.TryGetSafeReleasePageUri(
                releasePageUri,
                out var safeReleasePageUri))
        {
            return ReleasePageLaunchResult.Failure(
                "The release page address is unavailable or is not a trusted GitHub HTTPS URL.");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = safeReleasePageUri.AbsoluteUri,
                UseShellExecute = true,
                Verb = "open",
            });

            return ReleasePageLaunchResult.Success();
        }
        catch (Exception exception) when (exception is Win32Exception or
            InvalidOperationException or NotSupportedException or SecurityException)
        {
            return ReleasePageLaunchResult.Failure(
                ReleasePageAction.LaunchFailureMessage,
                exception);
        }
    }
}
