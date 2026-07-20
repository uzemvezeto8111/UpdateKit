using System.Buffers;
using System.Net;

namespace UpdateKit;

/// <summary>Streams HTTP release assets into safely committed destination files.</summary>
/// <remarks>The downloader uses but does not own or dispose the supplied <see cref="HttpClient"/>.</remarks>
public sealed class AssetDownloader
{
    private const int DefaultBufferSize = 81_920;

    private readonly ReleaseAssetRequestClient _requestClient;
    private readonly int _bufferSize;
    private readonly Action<string, string> _commitFile;
    private readonly DownloadRetryOptions _retryOptions;
    private readonly IDownloadDelay _delay;
    private readonly Func<double> _jitterSource;

    /// <summary>Creates a downloader that borrows the supplied HTTP client.</summary>
    public AssetDownloader(HttpClient httpClient)
        : this(httpClient, new DownloadRetryOptions())
    {
    }

    /// <summary>Creates a downloader with configurable transient-failure retries.</summary>
    public AssetDownloader(HttpClient httpClient, DownloadRetryOptions retryOptions)
        : this(
            new ReleaseAssetRequestClient(httpClient),
            DefaultBufferSize,
            CommitFile,
            retryOptions ?? throw new ArgumentNullException(nameof(retryOptions)))
    {
    }

    internal AssetDownloader(
        HttpClient httpClient,
        int bufferSize,
        Action<string, string>? commitFile = null)
        : this(
            new ReleaseAssetRequestClient(httpClient),
            bufferSize,
            commitFile)
    {
    }

    internal AssetDownloader(
        HttpClient httpClient,
        int bufferSize,
        Action<string, string>? commitFile,
        DownloadRetryOptions retryOptions,
        IDownloadDelay delay,
        Func<double> jitterSource)
        : this(
            new ReleaseAssetRequestClient(httpClient),
            bufferSize,
            commitFile,
            retryOptions,
            delay,
            jitterSource)
    {
    }

    internal AssetDownloader(
        ReleaseAssetRequestClient requestClient,
        int bufferSize = DefaultBufferSize,
        Action<string, string>? commitFile = null,
        DownloadRetryOptions? retryOptions = null,
        IDownloadDelay? delay = null,
        Func<double>? jitterSource = null)
    {
        ArgumentNullException.ThrowIfNull(requestClient);

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferSize),
                bufferSize,
                "The download buffer size must be positive.");
        }

        retryOptions ??= new DownloadRetryOptions();
        retryOptions.Validate();

        _requestClient = requestClient;
        _bufferSize = bufferSize;
        _commitFile = commitFile ?? CommitFile;
        _retryOptions = retryOptions;
        _delay = delay ?? SystemDownloadDelay.Instance;
        _jitterSource = jitterSource ?? Random.Shared.NextDouble;
    }

    /// <summary>
    /// Downloads an asset through a temporary file and commits the destination only after a complete transfer.
    /// </summary>
    public async Task<UpdateResult<DownloadResult>> DownloadAsync(
        ReleaseAsset asset,
        string? destinationFilePath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!_requestClient.TryCreatePlan(asset, out var requestPlan, out var requestValidationMessage))
        {
            return InvalidConfiguration(requestValidationMessage);
        }

        if (!TryResolveDestinationPath(
                destinationFilePath,
                out var resolvedDestinationPath,
                out var validationMessage))
        {
            return InvalidConfiguration(validationMessage);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Canceled(new OperationCanceledException(cancellationToken));
        }

        for (var retryNumber = 0; ;)
        {
            var attempt = await DownloadOnceAsync(
                    asset,
                    requestPlan,
                    resolvedDestinationPath,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (attempt.Result.IsSuccess ||
                !attempt.IsTransient ||
                retryNumber >= _retryOptions.MaxRetryAttempts)
            {
                return attempt.Result;
            }

            retryNumber++;
            var retryDelay = CalculateRetryDelay(retryNumber);
            _retryOptions.RetryProgress?.Report(
                new DownloadRetryAttempt(
                    retryNumber,
                    _retryOptions.MaxRetryAttempts,
                    retryDelay,
                    attempt.StatusCode,
                    attempt.Exception));

            try
            {
                await _delay.DelayAsync(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                return Canceled(exception);
            }
            catch (OperationCanceledException exception)
            {
                return DownloadFailure("The retry delay was canceled internally.", exception);
            }
        }
    }

    private async Task<DownloadAttemptOutcome> DownloadOnceAsync(
        ReleaseAsset asset,
        ReleaseAssetRequestPlan requestPlan,
        string resolvedDestinationPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        string? temporaryFilePath = null;

        try
        {
            using var assetResponse = await SendAssetRequestAsync(
                    requestPlan,
                    cancellationToken)
                .ConfigureAwait(false);
            var response = assetResponse.Response;

            if (!response.IsSuccessStatusCode)
            {
                return new DownloadAttemptOutcome(
                    HttpFailure(response, assetResponse.UsedAuthentication),
                    IsTransientStatusCode(response.StatusCode),
                    response.StatusCode);
            }

            var destinationDirectory = Path.GetDirectoryName(resolvedDestinationPath)!;
            temporaryFilePath = CreateTemporaryFilePath(destinationDirectory);
            var totalBytes = response.Content.Headers.ContentLength;

            await using var sourceStream = await OpenResponseStreamAsync(
                    response.Content,
                    cancellationToken)
                .ConfigureAwait(false);

            long bytesDownloaded;
            await using (var destinationStream = new FileStream(
                temporaryFilePath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = _bufferSize,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                }))
            {
                bytesDownloaded = await CopyToTemporaryFileAsync(
                    sourceStream,
                    destinationStream,
                    totalBytes,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            _commitFile(temporaryFilePath, resolvedDestinationPath);
            temporaryFilePath = null;

            return DownloadAttemptOutcome.Success(
                new DownloadResult(asset, resolvedDestinationPath, bytesDownloaded));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            return DownloadAttemptOutcome.Permanent(Canceled(exception));
        }
        catch (OperationCanceledException exception)
        {
            return DownloadAttemptOutcome.Permanent(
                DownloadFailure("The asset download timed out or was canceled internally.", exception));
        }
        catch (PermanentAssetRequestException exception)
        {
            return DownloadAttemptOutcome.Permanent(
                DownloadFailure("The asset request failed due to a network error.", exception));
        }
        catch (DownloadNetworkException exception)
        {
            return DownloadAttemptOutcome.Transient(
                DownloadFailure(exception.Message, exception.InnerException),
                exception.InnerException);
        }
        catch (HttpRequestException exception)
        {
            var failure = DownloadFailure("The asset request failed due to a network error.", exception);
            return IsTransientHttpRequestException(exception)
                ? DownloadAttemptOutcome.Transient(failure, exception, exception.StatusCode)
                : DownloadAttemptOutcome.Permanent(failure);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return DownloadAttemptOutcome.Permanent(
                DownloadFailure("The asset could not be written to the destination file.", exception));
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    private static async Task<Stream> OpenResponseStreamAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            return await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new DownloadNetworkException(
                "The asset response stream failed due to a network error.",
                exception);
        }
    }

    private async Task<ReleaseAssetResponse> SendAssetRequestAsync(
        ReleaseAssetRequestPlan requestPlan,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _requestClient
                .SendAsync(requestPlan, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new DownloadNetworkException(
                "The asset request failed due to a network error.",
                exception);
        }
    }

    private async Task<long> CopyToTemporaryFileAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        long bytesDownloaded = 0;

        try
        {
            progress?.Report(new DownloadProgress(bytesDownloaded, totalBytes));

            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = await source
                        .ReadAsync(buffer.AsMemory(0, _bufferSize), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    throw new DownloadNetworkException(
                        "The asset response stream failed due to a network error.",
                        exception);
                }

                if (bytesRead == 0)
                {
                    break;
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);

                bytesDownloaded = checked(bytesDownloaded + bytesRead);
                progress?.Report(new DownloadProgress(bytesDownloaded, totalBytes));
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report(new DownloadProgress(bytesDownloaded, totalBytes));
            return bytesDownloaded;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryResolveDestinationPath(
        string? destinationFilePath,
        out string resolvedDestinationPath,
        out string validationMessage)
    {
        resolvedDestinationPath = string.Empty;

        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            validationMessage = "A destination file path is required.";
            return false;
        }

        if (!Path.IsPathFullyQualified(destinationFilePath))
        {
            validationMessage = "The destination file path must be fully qualified.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(destinationFilePath);
            var fileName = Path.GetFileName(fullPath);
            var directory = Path.GetDirectoryName(fullPath);

            if (Path.EndsInDirectorySeparator(fullPath) ||
                Directory.Exists(fullPath) ||
                string.IsNullOrWhiteSpace(fileName))
            {
                validationMessage = "The destination path must identify a file, not a directory.";
                return false;
            }

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName[^1] is '.' or ' ')
            {
                validationMessage = "The destination file name contains invalid characters.";
                return false;
            }

            if (directory is null || !Directory.Exists(directory))
            {
                validationMessage = "The destination directory does not exist.";
                return false;
            }

            resolvedDestinationPath = fullPath;
            validationMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            validationMessage = "The destination file path is invalid.";
            return false;
        }
    }

    private static string CreateTemporaryFilePath(string destinationDirectory) =>
        Path.Combine(destinationDirectory, $".updatekit-{Guid.NewGuid():N}.tmp");

    private TimeSpan CalculateRetryDelay(int retryNumber)
    {
        var exponentialMultiplier = Math.Pow(2d, retryNumber - 1);
        var boundedTicks = Math.Min(
            _retryOptions.MaximumDelay.Ticks,
            _retryOptions.InitialDelay.Ticks * exponentialMultiplier);

        if (_retryOptions.JitterFactor > 0d && boundedTicks > 0d)
        {
            var sample = _jitterSource();
            if (!double.IsFinite(sample))
            {
                sample = 0.5d;
            }

            sample = Math.Clamp(sample, 0d, 1d);
            var jitterMultiplier =
                1d + ((sample * 2d) - 1d) * _retryOptions.JitterFactor;
            boundedTicks = Math.Min(
                _retryOptions.MaximumDelay.Ticks,
                boundedTicks * jitterMultiplier);
        }

        return TimeSpan.FromTicks((long)Math.Round(boundedTicks));
    }

    private static bool IsTransientHttpRequestException(HttpRequestException exception) =>
        exception.StatusCode is null || IsTransientStatusCode(exception.StatusCode.Value);

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static void CommitFile(string temporaryFilePath, string destinationFilePath)
    {
        if (File.Exists(destinationFilePath))
        {
            File.Replace(
                temporaryFilePath,
                destinationFilePath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryFilePath, destinationFilePath);
        }
    }

    private static void TryDeleteTemporaryFile(string? temporaryFilePath)
    {
        if (temporaryFilePath is null)
        {
            return;
        }

        try
        {
            File.Delete(temporaryFilePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Cleanup is best-effort when another process prevents deletion.
        }
    }

    private static UpdateResult<DownloadResult> InvalidConfiguration(string message) =>
        UpdateResult<DownloadResult>.Failure(
            new UpdateError(UpdateErrorCode.InvalidConfiguration, message));

    private static UpdateResult<DownloadResult> Canceled(OperationCanceledException exception) =>
        UpdateResult<DownloadResult>.Failure(
            new UpdateError(
                UpdateErrorCode.DownloadCanceled,
                "The asset download was canceled.",
                exception));

    private static UpdateResult<DownloadResult> DownloadFailure(
        string message,
        Exception? exception = null) =>
        UpdateResult<DownloadResult>.Failure(
            new UpdateError(UpdateErrorCode.DownloadFailed, message, exception));

    private static UpdateResult<DownloadResult> HttpFailure(
        HttpResponseMessage response,
        bool usedAuthentication)
    {
        if (usedAuthentication &&
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return UpdateResult<DownloadResult>.Failure(
                new UpdateError(
                    UpdateErrorCode.AuthenticationFailed,
                    "GitHub rejected the supplied credentials or release-asset permissions."));
        }

        return DownloadFailure(
            $"The asset request returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private sealed record DownloadAttemptOutcome(
        UpdateResult<DownloadResult> Result,
        bool IsTransient,
        HttpStatusCode? StatusCode = null,
        Exception? Exception = null)
    {
        public static DownloadAttemptOutcome Success(DownloadResult result) =>
            new(UpdateResult<DownloadResult>.Success(result), IsTransient: false);

        public static DownloadAttemptOutcome Permanent(UpdateResult<DownloadResult> result) =>
            new(result, IsTransient: false);

        public static DownloadAttemptOutcome Transient(
            UpdateResult<DownloadResult> result,
            Exception? exception = null,
            HttpStatusCode? statusCode = null) =>
            new(result, IsTransient: true, statusCode, exception);
    }

    private sealed class DownloadNetworkException : Exception
    {
        public DownloadNetworkException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
