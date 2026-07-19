namespace UpdateKit.WinForms.Internal;

internal enum UpdateDialogStatus
{
    Initial,
    Checking,
    NoUpdate,
    UpdateAvailable,
    Downloading,
    Canceling,
    Succeeded,
    Canceled,
    Failed,
}

internal sealed record UpdateDialogViewState(
    UpdateDialogStatus Status,
    UpdateCheckResult? CheckResult = null,
    ReleaseAsset? SelectedAsset = null,
    DownloadProgress? Progress = null,
    DownloadResult? DownloadResult = null,
    UpdateError? Error = null)
{
    public static UpdateDialogViewState Initial { get; } =
        new(UpdateDialogStatus.Initial);

    public bool IsBusy =>
        Status is UpdateDialogStatus.Checking or
            UpdateDialogStatus.Downloading or
            UpdateDialogStatus.Canceling;

    public bool CanCheck =>
        Status == UpdateDialogStatus.Initial ||
        Status is UpdateDialogStatus.Failed or UpdateDialogStatus.Canceled &&
            CheckResult is null;

    public bool CanDownload =>
        SelectedAsset is not null &&
        Status is UpdateDialogStatus.UpdateAvailable or
            UpdateDialogStatus.Failed or
            UpdateDialogStatus.Canceled;

    public bool CanCancel =>
        Status is UpdateDialogStatus.Checking or UpdateDialogStatus.Downloading;

    public bool CanClose => !IsBusy;
}
