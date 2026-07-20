using System.Security.Cryptography;

namespace UpdateKit;

/// <summary>Performs streaming SHA-256 verification for completed downloads.</summary>
/// <remarks>The verifier uses but does not own or dispose the supplied <see cref="HttpClient"/>.</remarks>
public sealed class Sha256Verifier
{
    private const int FileBufferSize = 81_920;

    private readonly HttpChecksumFileSource _checksumFileSource;
    private readonly Func<string, Stream> _openFile;
    private readonly Action<string> _deleteFile;

    /// <summary>Creates a verifier whose checksum-file requests borrow the supplied HTTP client.</summary>
    public Sha256Verifier(HttpClient httpClient)
        : this(new ReleaseAssetRequestClient(httpClient), OpenFile, File.Delete)
    {
    }

    internal Sha256Verifier(
        HttpClient httpClient,
        Func<string, Stream>? openFile,
        Action<string>? deleteFile)
        : this(
            new ReleaseAssetRequestClient(httpClient),
            openFile ?? throw new ArgumentNullException(nameof(openFile)),
            deleteFile ?? throw new ArgumentNullException(nameof(deleteFile)))
    {
    }

    internal Sha256Verifier(
        ReleaseAssetRequestClient requestClient,
        Func<string, Stream>? openFile = null,
        Action<string>? deleteFile = null)
    {
        _checksumFileSource = new HttpChecksumFileSource(requestClient);
        _openFile = openFile ?? OpenFile;
        _deleteFile = deleteFile ?? File.Delete;
    }

    /// <summary>Verifies a download against a 64-character hexadecimal SHA-256 checksum.</summary>
    public Task<UpdateResult<DownloadResult>> VerifyAsync(
        DownloadResult download,
        string? expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);

        if (!Sha256Checksum.TryParse(expectedSha256, out var expectedChecksum))
        {
            return Task.FromResult(
                Failure(
                    UpdateErrorCode.InvalidChecksum,
                    "The expected SHA-256 checksum must contain exactly 64 hexadecimal characters."));
        }

        return VerifyCoreAsync(download, expectedChecksum, cancellationToken);
    }

    /// <summary>Retrieves a checksum-file asset and verifies the entry matching the downloaded asset name.</summary>
    public async Task<UpdateResult<DownloadResult>> VerifyFromChecksumFileAsync(
        DownloadResult download,
        ReleaseAsset checksumAsset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);
        ArgumentNullException.ThrowIfNull(checksumAsset);

        var contentResult = await _checksumFileSource
            .GetContentAsync(checksumAsset, cancellationToken)
            .ConfigureAwait(false);

        if (!contentResult.IsSuccess)
        {
            return UpdateResult<DownloadResult>.Failure(contentResult.Error);
        }

        var checksumResult = ChecksumFileParser.FindSha256(
            contentResult.Value,
            download.Asset.Name);

        if (!checksumResult.IsSuccess)
        {
            return UpdateResult<DownloadResult>.Failure(checksumResult.Error);
        }

        return await VerifyCoreAsync(
                download,
                checksumResult.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<UpdateResult<DownloadResult>> VerifyCoreAsync(
        DownloadResult download,
        byte[] expectedChecksum,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Canceled(new OperationCanceledException(cancellationToken));
        }

        byte[] actualChecksum;

        try
        {
            await using var stream = _openFile(download.FilePath);
            actualChecksum = await SHA256
                .HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            return Canceled(exception);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                CryptographicException or
                OperationCanceledException)
        {
            return Failure(
                UpdateErrorCode.FileSystemError,
                "The downloaded file could not be read for SHA-256 verification.",
                exception);
        }

        if (Sha256Checksum.Equals(actualChecksum, expectedChecksum))
        {
            return UpdateResult<DownloadResult>.Success(download);
        }

        try
        {
            _deleteFile(download.FilePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Failure(
                UpdateErrorCode.FileSystemError,
                "The SHA-256 checksum did not match and the invalid download could not be deleted.",
                exception);
        }

        return Failure(
            UpdateErrorCode.ChecksumMismatch,
            "The downloaded file's SHA-256 checksum does not match the expected value.");
    }

    private static Stream OpenFile(string filePath) =>
        new FileStream(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = FileBufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

    private static UpdateResult<DownloadResult> Canceled(
        OperationCanceledException exception) =>
        Failure(
            UpdateErrorCode.DownloadCanceled,
            "SHA-256 verification was canceled.",
            exception);

    private static UpdateResult<DownloadResult> Failure(
        UpdateErrorCode code,
        string message,
        Exception? exception = null) =>
        UpdateResult<DownloadResult>.Failure(new UpdateError(code, message, exception));
}
