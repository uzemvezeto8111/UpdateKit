namespace UpdateKit.Wpf.Internal;

internal sealed record UpdateWindowState(
    UpdateWindowStatus Status,
    UpdateCheckResult? CheckResult = null,
    ReleaseAsset? SelectedAsset = null,
    DownloadProgress? Progress = null,
    DownloadResult? DownloadResult = null,
    UpdateError? Error = null)
{
    public static UpdateWindowState Initial { get; } =
        new(UpdateWindowStatus.Initial);

    public bool IsBusy =>
        Status is UpdateWindowStatus.Checking or
            UpdateWindowStatus.Downloading or
            UpdateWindowStatus.Canceling;

    public bool CanCheck =>
        Status == UpdateWindowStatus.Initial ||
        Status is UpdateWindowStatus.Failed or UpdateWindowStatus.Canceled &&
            CheckResult is null;

    public bool CanDownload =>
        SelectedAsset is not null &&
        Status is UpdateWindowStatus.UpdateAvailable or
            UpdateWindowStatus.Failed or
            UpdateWindowStatus.Canceled;

    public bool CanCancel =>
        Status is UpdateWindowStatus.Checking or UpdateWindowStatus.Downloading;

    public bool CanClose => !IsBusy;
}
