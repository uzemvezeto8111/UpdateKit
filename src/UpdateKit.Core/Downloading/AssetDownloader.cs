using System.Buffers;
using System.Net;

namespace UpdateKit;

/// <summary>Streams HTTP release assets into safely committed destination files.</summary>
/// <remarks>The downloader uses but does not own or dispose the supplied <see cref="HttpClient"/>.</remarks>
public sealed class AssetDownloader
{
    private const int DefaultBufferSize = 81_920;

    private readonly HttpClient _httpClient;
    private readonly int _bufferSize;
    private readonly Action<string, string> _commitFile;

    /// <summary>Creates a downloader that borrows the supplied HTTP client.</summary>
    public AssetDownloader(HttpClient httpClient)
        : this(httpClient, DefaultBufferSize, CommitFile)
    {
    }

    internal AssetDownloader(
        HttpClient httpClient,
        int bufferSize,
        Action<string, string>? commitFile = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferSize),
                bufferSize,
                "The download buffer size must be positive.");
        }

        _httpClient = httpClient;
        _bufferSize = bufferSize;
        _commitFile = commitFile ?? CommitFile;
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

        if (!IsSupportedDownloadUri(asset.DownloadUrl))
        {
            return InvalidConfiguration("The asset download URL must use HTTP or HTTPS.");
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

        string? temporaryFilePath = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return DownloadFailure(
                    $"The asset request returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var destinationDirectory = Path.GetDirectoryName(resolvedDestinationPath)!;
            temporaryFilePath = CreateTemporaryFilePath(destinationDirectory);
            var totalBytes = response.Content.Headers.ContentLength;

            await using var sourceStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
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

            return UpdateResult<DownloadResult>.Success(
                new DownloadResult(asset, resolvedDestinationPath, bytesDownloaded));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            return Canceled(exception);
        }
        catch (OperationCanceledException exception)
        {
            return DownloadFailure("The asset download timed out or was canceled internally.", exception);
        }
        catch (HttpRequestException exception)
        {
            return DownloadFailure("The asset request failed due to a network error.", exception);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return DownloadFailure("The asset could not be written to the destination file.", exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
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
                var bytesRead = await source
                    .ReadAsync(buffer.AsMemory(0, _bufferSize), cancellationToken)
                    .ConfigureAwait(false);

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

    private static bool IsSupportedDownloadUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static string CreateTemporaryFilePath(string destinationDirectory) =>
        Path.Combine(destinationDirectory, $".updatekit-{Guid.NewGuid():N}.tmp");

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
}
