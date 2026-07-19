namespace UpdateKit.WinForms.Internal;

internal sealed class UpdateDialogController : IDisposable
{
    private readonly object _sync = new();
    private readonly UpdateDialogOptions _options;

    private UpdateDialogViewState _state = UpdateDialogViewState.Initial;
    private CancellationTokenSource? _operationCancellation;
    private Action<UpdateDialogViewState>? _stateChanged;
    private bool _disposed;

    public UpdateDialogController(UpdateDialogOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    public event Action<UpdateDialogViewState> StateChanged
    {
        add
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _stateChanged += value;
            }
        }
        remove
        {
            lock (_sync)
            {
                _stateChanged -= value;
            }
        }
    }

    public UpdateDialogViewState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

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
                .CheckForUpdateAsync(_options.CurrentVersion, cancellation.Token)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                SetState(new UpdateDialogViewState(
                    UpdateDialogStatus.Failed,
                    Error: result.Error));
                return true;
            }

            if (!result.Value.IsUpdateAvailable)
            {
                SetState(new UpdateDialogViewState(
                    UpdateDialogStatus.NoUpdate,
                    CheckResult: result.Value));
                return true;
            }

            var assetResult = _options.AssetSelector(result.Value.LatestRelease);
            if (assetResult is null)
            {
                throw new InvalidOperationException("The asset selector returned no result.");
            }

            if (!assetResult.IsSuccess)
            {
                SetState(new UpdateDialogViewState(
                    UpdateDialogStatus.Failed,
                    CheckResult: result.Value,
                    Error: assetResult.Error));
                return true;
            }

            SetState(new UpdateDialogViewState(
                UpdateDialogStatus.UpdateAvailable,
                result.Value,
                assetResult.Value));
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            SetState(new UpdateDialogViewState(
                UpdateDialogStatus.Canceled,
                Error: CanceledError("The update check was canceled.", exception)));
        }
        catch (Exception exception)
        {
            SetState(new UpdateDialogViewState(
                UpdateDialogStatus.Failed,
                Error: UnexpectedError("The update check could not be completed.", exception)));
        }
        finally
        {
            EndOperation(cancellation);
        }

        return true;
    }

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
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            else if (_options.ChecksumAssetSelector is not null)
            {
                var checksumAssetResult = _options.ChecksumAssetSelector(
                    checkResult.LatestRelease);

                if (checksumAssetResult is null)
                {
                    throw new InvalidOperationException(
                        "The checksum-file asset selector returned no result.");
                }

                if (!checksumAssetResult.IsSuccess)
                {
                    SetState(new UpdateDialogViewState(
                        UpdateDialogStatus.Failed,
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
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await _options.Client.DownloadAsync(
                        asset,
                        _options.DestinationFilePath,
                        progress,
                        cancellation.Token)
                    .ConfigureAwait(false);
            }

            if (result.IsSuccess)
            {
                SetState(new UpdateDialogViewState(
                    UpdateDialogStatus.Succeeded,
                    checkResult,
                    asset,
                    State.Progress,
                    result.Value));
            }
            else
            {
                var status = result.Error.Code == UpdateErrorCode.DownloadCanceled
                    ? UpdateDialogStatus.Canceled
                    : UpdateDialogStatus.Failed;
                SetState(new UpdateDialogViewState(
                    status,
                    checkResult,
                    asset,
                    State.Progress,
                    Error: result.Error));
            }
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            SetState(new UpdateDialogViewState(
                UpdateDialogStatus.Canceled,
                checkResult,
                asset,
                State.Progress,
                Error: CanceledError("The update download was canceled.", exception)));
        }
        catch (Exception exception)
        {
            SetState(new UpdateDialogViewState(
                UpdateDialogStatus.Failed,
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

    public bool CancelOperation()
    {
        UpdateDialogViewState nextState;
        Action<UpdateDialogViewState>? handler;

        lock (_sync)
        {
            if (_disposed || !_state.CanCancel || _operationCancellation is null)
            {
                return false;
            }

            nextState = _state with { Status = UpdateDialogStatus.Canceling };
            _state = nextState;
            handler = _stateChanged;
            _operationCancellation.Cancel();
        }

        handler?.Invoke(nextState);
        return true;
    }

    public bool RequestClose()
    {
        if (!State.IsBusy)
        {
            return true;
        }

        CancelOperation();
        return false;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stateChanged = null;
            _operationCancellation?.Cancel();
        }
    }

    private CancellationTokenSource? BeginCheck()
    {
        UpdateDialogViewState nextState;
        Action<UpdateDialogViewState>? handler;
        CancellationTokenSource cancellation;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_state.CanCheck || _operationCancellation is not null)
            {
                return null;
            }

            cancellation = new CancellationTokenSource();
            _operationCancellation = cancellation;
            nextState = new UpdateDialogViewState(UpdateDialogStatus.Checking);
            _state = nextState;
            handler = _stateChanged;
        }

        handler?.Invoke(nextState);
        return cancellation;
    }

    private (
        CancellationTokenSource Cancellation,
        UpdateCheckResult CheckResult,
        ReleaseAsset Asset)? BeginDownload()
    {
        UpdateDialogViewState nextState;
        Action<UpdateDialogViewState>? handler;
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
            nextState = new UpdateDialogViewState(
                UpdateDialogStatus.Downloading,
                checkResult,
                asset,
                new DownloadProgress(0, asset.Size));
            _state = nextState;
            handler = _stateChanged;
        }

        handler?.Invoke(nextState);
        return (cancellation, checkResult, asset);
    }

    private void ReportProgress(DownloadProgress progress)
    {
        UpdateDialogViewState nextState;
        Action<UpdateDialogViewState>? handler;

        lock (_sync)
        {
            if (_disposed ||
                _state.Status is not (UpdateDialogStatus.Downloading or UpdateDialogStatus.Canceling))
            {
                return;
            }

            nextState = _state with { Progress = progress };
            _state = nextState;
            handler = _stateChanged;
        }

        handler?.Invoke(nextState);
    }

    private void SetState(UpdateDialogViewState state)
    {
        Action<UpdateDialogViewState>? handler;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _state = state;
            handler = _stateChanged;
        }

        handler?.Invoke(state);
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
