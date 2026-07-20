using System.Diagnostics;

namespace UpdateKit.Example.WinForms.Services;

internal interface IDestinationFolderLauncher
{
    FolderLaunchResult OpenContainingFolder(string filePath);
}

internal sealed record FolderLaunchResult(bool IsSuccess, string? ErrorMessage = null);

internal sealed class DestinationFolderLauncher : IDestinationFolderLauncher
{
    public FolderLaunchResult OpenContainingFolder(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory is null || !Directory.Exists(directory))
            {
                return new(false, "The destination folder no longer exists.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
                Verb = "open",
            });
            return new(true);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return new(false, exception.Message);
        }
    }
}

internal sealed class DestinationFolderCompletionAction
{
    private readonly IDestinationFolderLauncher _launcher;

    public DestinationFolderCompletionAction(IDestinationFolderLauncher launcher) =>
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public FolderLaunchResult? Run(bool enabled, string? successfullyDownloadedFilePath) =>
        enabled && successfullyDownloadedFilePath is not null
            ? _launcher.OpenContainingFolder(successfullyDownloadedFilePath)
            : null;
}
