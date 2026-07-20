using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpdateKit.Desktop.Internal;
using UpdateKit.Wpf.Internal;

namespace UpdateKit.Wpf;

/// <summary>
/// Bindable state and commands for one update-window workflow. The view model borrows and does not
/// dispose the <see cref="UpdateClient"/> configured through <see cref="UpdateWindowOptions"/>.
/// </summary>
public sealed class UpdateWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string[] StatePropertyNames =
    [
        nameof(Status),
        nameof(CheckResult),
        nameof(SelectedAsset),
        nameof(DownloadProgress),
        nameof(DownloadResult),
        nameof(LastError),
        nameof(IsOperationInProgress),
        nameof(CanCheck),
        nameof(CanDownload),
        nameof(CanCancel),
        nameof(CanClose),
        nameof(IsViewReleaseVisible),
        nameof(CanViewRelease),
        nameof(Heading),
        nameof(ReleaseName),
        nameof(AvailableVersion),
        nameof(PublishedText),
        nameof(SelectedAssetText),
        nameof(ReleaseNotes),
        nameof(StatusText),
        nameof(ProgressPercentage),
        nameof(IsProgressIndeterminate),
        nameof(ProgressText),
        nameof(ErrorText),
        nameof(HasError),
        nameof(PrimaryActionText),
    ];

    private readonly object _sync = new();
    private readonly UpdateWindowOptions _options;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly AsyncRelayCommand _checkForUpdateCommand;
    private readonly AsyncRelayCommand _downloadCommand;
    private readonly AsyncRelayCommand _primaryActionCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _viewReleaseCommand;
    private readonly ReleasePageAction _releasePageAction;

    private UpdateWindowState _state = UpdateWindowState.Initial;
    private CancellationTokenSource? _operationCancellation;
    private string? _releasePageError;
    private bool _disposed;

    /// <summary>Creates a bindable workflow for the supplied window options.</summary>
    /// <remarks>Create the view model on the UI thread when property notifications must return there.</remarks>
    public UpdateWindowViewModel(UpdateWindowOptions options)
        : this(options, new ShellReleasePageLauncher())
    {
    }

    internal UpdateWindowViewModel(
        UpdateWindowOptions options,
        IReleasePageLauncher releasePageLauncher)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        _releasePageAction = new ReleasePageAction(releasePageLauncher);
        _synchronizationContext = SynchronizationContext.Current;

        _checkForUpdateCommand = new AsyncRelayCommand(
            async () => await CheckForUpdateAsync(),
            () => CanCheck);
        _downloadCommand = new AsyncRelayCommand(
            async () => await DownloadAsync(),
            () => CanDownload);
        _primaryActionCommand = new AsyncRelayCommand(
            ExecutePrimaryActionAsync,
            () => CanCheck || CanDownload);
        _cancelCommand = new RelayCommand(
            () => CancelOperation(),
            () => CanCancel);
        _viewReleaseCommand = new RelayCommand(
            OpenReleasePage,
            () => CanViewRelease);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the current workflow status.</summary>
    public UpdateWindowStatus Status => State.Status;

    /// <summary>Gets the latest completed check result, when available.</summary>
    public UpdateCheckResult? CheckResult => State.CheckResult;

    /// <summary>Gets the selected primary release asset, when available.</summary>
    public ReleaseAsset? SelectedAsset => State.SelectedAsset;

    /// <summary>Gets the most recent download progress measurement.</summary>
    public DownloadProgress? DownloadProgress => State.Progress;

    /// <summary>Gets the successfully completed download result, when available.</summary>
    public DownloadResult? DownloadResult => State.DownloadResult;

    /// <summary>Gets the most recent operational error, when available.</summary>
    public UpdateError? LastError => State.Error;

    /// <summary>Gets whether a check, download, verification, or cancellation is active.</summary>
    public bool IsOperationInProgress => State.IsBusy;

    /// <summary>Gets whether an update check can start.</summary>
    public bool CanCheck => State.CanCheck;

    /// <summary>Gets whether the selected release asset can be downloaded.</summary>
    public bool CanDownload => State.CanDownload;

    /// <summary>Gets whether the active operation can be canceled.</summary>
    public bool CanCancel => State.CanCancel;

    /// <summary>Gets whether the host can close without first waiting for cancellation.</summary>
    public bool CanClose => State.CanClose;

    /// <summary>Gets whether the standard window can display the secure GitHub release-page action.</summary>
    public bool IsViewReleaseVisible => _releasePageAction.IsVisible(
        CheckResult?.LatestRelease.HtmlUrl);

    /// <summary>Gets whether the secure GitHub release page can be opened in the current state.</summary>
    public bool CanViewRelease => IsViewReleaseVisible && !IsOperationInProgress;

    /// <summary>Gets the command that starts an update check.</summary>
    public ICommand CheckForUpdateCommand => _checkForUpdateCommand;

    /// <summary>Gets the command that starts the configured download and verification workflow.</summary>
    public ICommand DownloadCommand => _downloadCommand;

    /// <summary>Gets the context-sensitive check, retry, or download command used by the standard window.</summary>
    public ICommand PrimaryActionCommand => _primaryActionCommand;

    /// <summary>Gets the command that cancels the active operation.</summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>Gets the command that opens the validated GitHub release page in the default browser.</summary>
    public ICommand ViewReleaseCommand => _viewReleaseCommand;

    /// <summary>Gets the localized status heading.</summary>
    public string Heading => HeadingFor(Status);

    /// <summary>Gets the release display name.</summary>
    public string ReleaseName => CheckResult?.LatestRelease.Name ?? "No release selected";

    /// <summary>Gets the release tag for display.</summary>
    public string AvailableVersion => CheckResult?.LatestRelease.TagName ?? "—";

    /// <summary>Gets the locally formatted publication timestamp.</summary>
    public string PublishedText => CheckResult?.LatestRelease.PublishedAt?.ToLocalTime()
        .ToString("g", CultureInfo.CurrentCulture) ?? "—";

    /// <summary>Gets the selected asset name and size for display.</summary>
    public string SelectedAssetText => SelectedAsset is null
        ? "—"
        : $"{SelectedAsset.Name} ({FormatBytes(SelectedAsset.Size)})";

    /// <summary>Gets release notes or a descriptive fallback.</summary>
    public string ReleaseNotes => string.IsNullOrWhiteSpace(CheckResult?.LatestRelease.Body)
        ? "No release notes were provided."
        : CheckResult.LatestRelease.Body;

    /// <summary>Gets the current operation status for display.</summary>
    public string StatusText => StatusTextFor(State);

    /// <summary>Gets progress from zero through one hundred.</summary>
    public double ProgressPercentage => Status == UpdateWindowStatus.Succeeded
        ? 100d
        : DownloadProgress?.Percentage ?? 0d;

    /// <summary>Gets whether the standard progress indicator should be indeterminate.</summary>
    public bool IsProgressIndeterminate =>
        Status == UpdateWindowStatus.Checking ||
        Status is UpdateWindowStatus.Downloading or UpdateWindowStatus.Canceling &&
            DownloadProgress?.TotalBytes is null;

    /// <summary>Gets downloaded and total bytes for display.</summary>
    public string ProgressText => DownloadProgress switch
    {
        { TotalBytes: { } total } progress =>
            $"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(total)}",
        { } progress => $"{FormatBytes(progress.BytesDownloaded)} downloaded",
        _ => string.Empty,
    };

    /// <summary>Gets the actionable error text.</summary>
    public string ErrorText
    {
        get
        {
            string? releasePageError;
            lock (_sync)
            {
                releasePageError = _releasePageError;
            }

            return releasePageError is not null
                ? $"Error: {releasePageError}"
                : LastError is null ? string.Empty : $"Error: {LastError.Message}";
        }
    }

    /// <summary>Gets whether an operational error is available.</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>Gets the context-sensitive primary action label.</summary>
    public string PrimaryActionText => CanCheck
        ? Status == UpdateWindowStatus.Initial ? "Check for updates" : "Try again"
        : Status is UpdateWindowStatus.Failed or UpdateWindowStatus.Canceled
            ? "Try again"
            : "Download";

    private UpdateWindowState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    /// <summary>Starts an update check when the current state permits it.</summary>
    /// <returns><see langword="true"/> when an operation was started; otherwise <see langword="false"/>.</returns>
    public async Task<bool> CheckForUpdateAsync()
    {
        var cancellation = BeginCheck();
        if (cancellation is null)
        {
            return false;
        }

        try
        {
            var result = await _options.Client
                .CheckForUpdateAsync(_options.CurrentVersion, cancellation.Token);

            if (!result.IsSuccess)
            {
                SetState(new UpdateWindowState(
                    UpdateWindowStatus.Failed,
                    Error: result.Error));
                return true;
            }

            if (!result.Value.IsUpdateAvailable)
            {
                SetState(new UpdateWindowState(
                    UpdateWindowStatus.NoUpdate,
                    CheckResult: result.Value));
                return true;
            }

            var assetResult = _options.AssetSelector(result.Value.LatestRelease) ??
                throw new InvalidOperationException("The asset selector returned no result.");

            if (!assetResult.IsSuccess)
            {
                SetState(new UpdateWindowState(
                    UpdateWindowStatus.Failed,
                    CheckResult: result.Value,
                    Error: assetResult.Error));
                return true;
            }

            SetState(new UpdateWindowState(
                UpdateWindowStatus.UpdateAvailable,
                result.Value,
                assetResult.Value));
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            SetState(new UpdateWindowState(
                UpdateWindowStatus.Canceled,
                Error: CanceledError("The update check was canceled.", exception)));
        }
        catch (Exception exception)
        {
            SetState(new UpdateWindowState(
                UpdateWindowStatus.Failed,
                Error: UnexpectedError("The update check could not be completed.", exception)));
        }
        finally
        {
            EndOperation(cancellation);
        }

        return true;
    }

    /// <summary>Starts the configured download and verification workflow when the current state permits it.</summary>
    /// <returns><see langword="true"/> when an operation was started; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DownloadAsync()
    {
        var operation = BeginDownload();
        if (operation is null)
        {
            return false;
        }

        var (cancellation, checkResult, asset) = operation.Value;

        try
        {
            var progress = new InlineProgress<DownloadProgress>(ReportProgress);
            UpdateResult<DownloadResult> result;

            if (_options.ExpectedSha256 is not null)
            {
                result = await _options.Client.DownloadAndVerifyAsync(
                    asset,
                    _options.DestinationFilePath,
                    _options.ExpectedSha256,
                    progress,
                    cancellation.Token);
            }
            else if (_options.ChecksumAssetSelector is not null)
            {
                var checksumAssetResult = _options.ChecksumAssetSelector(
                    checkResult.LatestRelease) ??
                    throw new InvalidOperationException(
                        "The checksum-file asset selector returned no result.");

                if (!checksumAssetResult.IsSuccess)
                {
                    SetState(new UpdateWindowState(
                        UpdateWindowStatus.Failed,
                        checkResult,
                        asset,
                        Error: checksumAssetResult.Error));
                    return true;
                }

                result = await _options.Client.DownloadAndVerifyFromChecksumFileAsync(
                    asset,
                    _options.DestinationFilePath,
                    checksumAssetResult.Value,
                    progress,
                    cancellation.Token);
            }
            else
            {
                result = await _options.Client.DownloadAsync(
                    asset,
                    _options.DestinationFilePath,
                    progress,
                    cancellation.Token);
            }

            if (result.IsSuccess)
            {
                SetState(new UpdateWindowState(
                    UpdateWindowStatus.Succeeded,
                    checkResult,
                    asset,
                    State.Progress,
                    result.Value));
            }
            else
            {
                var status = result.Error.Code == UpdateErrorCode.DownloadCanceled
                    ? UpdateWindowStatus.Canceled
                    : UpdateWindowStatus.Failed;
                SetState(new UpdateWindowState(
                    status,
                    checkResult,
                    asset,
                    State.Progress,
                    Error: result.Error));
            }
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            SetState(new UpdateWindowState(
                UpdateWindowStatus.Canceled,
                checkResult,
                asset,
                State.Progress,
                Error: CanceledError("The update download was canceled.", exception)));
        }
        catch (Exception exception)
        {
            SetState(new UpdateWindowState(
                UpdateWindowStatus.Failed,
                checkResult,
                asset,
                State.Progress,
                Error: UnexpectedError("The update download could not be completed.", exception)));
        }
        finally
        {
            EndOperation(cancellation);
        }

        return true;
    }

    /// <summary>Requests cancellation of the current operation.</summary>
    /// <returns><see langword="true"/> when cancellation was requested; otherwise <see langword="false"/>.</returns>
    public bool CancelOperation()
    {
        UpdateWindowState nextState;
        CancellationTokenSource cancellation;

        lock (_sync)
        {
            if (_disposed || !_state.CanCancel || _operationCancellation is null)
            {
                return false;
            }

            nextState = _state with { Status = UpdateWindowStatus.Canceling };
            _state = nextState;
            cancellation = _operationCancellation;
        }

        cancellation.Cancel();
        RaiseStateChanged();
        return true;
    }

    /// <summary>Returns whether the window may close immediately, canceling active work when necessary.</summary>
    public bool RequestClose()
    {
        if (!IsOperationInProgress)
        {
            return true;
        }

        CancelOperation();
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancellationTokenSource? cancellation;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cancellation = _operationCancellation;
        }

        cancellation?.Cancel();
    }

    private async Task ExecutePrimaryActionAsync()
    {
        if (CanCheck)
        {
            await CheckForUpdateAsync();
        }
        else if (CanDownload)
        {
            await DownloadAsync();
        }
    }

    private void OpenReleasePage()
    {
        var result = _releasePageAction.TryOpen(CheckResult?.LatestRelease.HtmlUrl);

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _releasePageError = result.IsSuccess ? null : result.ErrorMessage;
        }

        RaiseStateChanged();
    }

    private CancellationTokenSource? BeginCheck()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_state.CanCheck || _operationCancellation is not null)
            {
                return null;
            }

            _operationCancellation = new CancellationTokenSource();
            _releasePageError = null;
            _state = new UpdateWindowState(UpdateWindowStatus.Checking);
        }

        RaiseStateChanged();
        return _operationCancellation;
    }

    private (
        CancellationTokenSource Cancellation,
        UpdateCheckResult CheckResult,
        ReleaseAsset Asset)? BeginDownload()
    {
        CancellationTokenSource cancellation;
        UpdateCheckResult checkResult;
        ReleaseAsset asset;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_state.CanDownload ||
                _operationCancellation is not null ||
                _state.CheckResult is null ||
                _state.SelectedAsset is null)
            {
                return null;
            }

            checkResult = _state.CheckResult;
            asset = _state.SelectedAsset;
            cancellation = new CancellationTokenSource();
            _operationCancellation = cancellation;
            _releasePageError = null;
            _state = new UpdateWindowState(
                UpdateWindowStatus.Downloading,
                checkResult,
                asset,
                new DownloadProgress(0, asset.Size));
        }

        RaiseStateChanged();
        return (cancellation, checkResult, asset);
    }

    private void ReportProgress(DownloadProgress progress)
    {
        lock (_sync)
        {
            if (_disposed ||
                _state.Status is not (UpdateWindowStatus.Downloading or UpdateWindowStatus.Canceling))
            {
                return;
            }

            _state = _state with { Progress = progress };
        }

        RaiseStateChanged();
    }

    private void SetState(UpdateWindowState state)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _state = state;
        }

        RaiseStateChanged();
    }

    private void EndOperation(CancellationTokenSource cancellation)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
            }
        }

        cancellation.Dispose();
    }

    private void RaiseStateChanged()
    {
        void Notify()
        {
            foreach (var propertyName in StatePropertyNames)
            {
                OnPropertyChanged(propertyName);
            }

            _checkForUpdateCommand.RaiseCanExecuteChanged();
            _downloadCommand.RaiseCanExecuteChanged();
            _primaryActionCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
            _viewReleaseCommand.RaiseCanExecuteChanged();
        }

        if (_synchronizationContext is null ||
            ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            Notify();
        }
        else
        {
            _synchronizationContext.Post(_ => Notify(), null);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string HeadingFor(UpdateWindowStatus status) => status switch
    {
        UpdateWindowStatus.Initial => "Check for updates",
        UpdateWindowStatus.Checking => "Checking for updates…",
        UpdateWindowStatus.NoUpdate => "You're up to date",
        UpdateWindowStatus.UpdateAvailable => "An update is available",
        UpdateWindowStatus.Downloading => "Downloading update…",
        UpdateWindowStatus.Canceling => "Canceling…",
        UpdateWindowStatus.Succeeded => "Update downloaded",
        UpdateWindowStatus.Canceled => "Operation canceled",
        UpdateWindowStatus.Failed => "Update failed",
        _ => "Software update",
    };

    private static string StatusTextFor(UpdateWindowState state) => state.Status switch
    {
        UpdateWindowStatus.Initial => "Ready to check for an update.",
        UpdateWindowStatus.Checking => "Contacting the release service…",
        UpdateWindowStatus.NoUpdate =>
            $"Version {state.CheckResult?.CurrentVersion} is already current.",
        UpdateWindowStatus.UpdateAvailable => "Review the release and choose Download to continue.",
        UpdateWindowStatus.Downloading => "Downloading the selected release asset…",
        UpdateWindowStatus.Canceling => "Waiting for the active operation to stop…",
        UpdateWindowStatus.Succeeded =>
            $"The update was saved to {state.DownloadResult?.FilePath}.",
        UpdateWindowStatus.Canceled => "The operation was canceled safely.",
        UpdateWindowStatus.Failed => "The operation could not be completed.",
        _ => string.Empty,
    };

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1_024 && unitIndex < units.Length - 1)
        {
            value /= 1_024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes.ToString("N0", CultureInfo.CurrentCulture)} {units[unitIndex]}"
            : $"{value.ToString("N1", CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }

    private static UpdateError CanceledError(
        string message,
        OperationCanceledException exception) =>
        new(UpdateErrorCode.DownloadCanceled, message, exception);

    private static UpdateError UnexpectedError(string message, Exception exception) =>
        new(UpdateErrorCode.Unknown, message, exception);

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public InlineProgress(Action<T> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public void Report(T value) => _callback(value);
    }
}
