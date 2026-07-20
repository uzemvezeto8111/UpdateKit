using System.Text;

namespace UpdateKit;

internal sealed class HttpChecksumFileSource
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReleaseAssetRequestClient _requestClient;

    public HttpChecksumFileSource(HttpClient httpClient)
        : this(new ReleaseAssetRequestClient(httpClient))
    {
    }

    public HttpChecksumFileSource(ReleaseAssetRequestClient requestClient)
    {
        _requestClient = requestClient ?? throw new ArgumentNullException(nameof(requestClient));
    }

    public async Task<UpdateResult<string>> GetContentAsync(
        ReleaseAsset checksumAsset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checksumAsset);

        if (!_requestClient.TryCreatePlan(
                checksumAsset,
                out var requestPlan,
                out var requestValidationMessage))
        {
            return Failure(
                UpdateErrorCode.InvalidConfiguration,
                requestValidationMessage);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Canceled(new OperationCanceledException(cancellationToken));
        }

        try
        {
            using var assetResponse = await _requestClient
                .SendAsync(requestPlan, cancellationToken)
                .ConfigureAwait(false);
            var response = assetResponse.Response;

            if (!response.IsSuccessStatusCode)
            {
                if (assetResponse.UsedAuthentication &&
                    response.StatusCode is (
                        System.Net.HttpStatusCode.Unauthorized or
                        System.Net.HttpStatusCode.Forbidden))
                {
                    return Failure(
                        UpdateErrorCode.AuthenticationFailed,
                        "GitHub rejected the supplied credentials or checksum-asset permissions.");
                }

                return Failure(
                    UpdateErrorCode.NetworkError,
                    $"The checksum-file request returned HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            await using var contentStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var reader = new StreamReader(
                contentStream,
                StrictUtf8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1_024,
                leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            return UpdateResult<string>.Success(content);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            return Canceled(exception);
        }
        catch (DecoderFallbackException exception)
        {
            return Failure(
                UpdateErrorCode.InvalidChecksum,
                "The checksum file is not valid UTF-8 text.",
                exception);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(
                UpdateErrorCode.NetworkError,
                "The checksum-file request timed out or was canceled internally.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            return Failure(
                UpdateErrorCode.NetworkError,
                "The checksum-file request failed due to a network error.",
                exception);
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException)
        {
            return Failure(
                UpdateErrorCode.NetworkError,
                "The checksum-file response could not be read.",
                exception);
        }
    }

    private static UpdateResult<string> Canceled(OperationCanceledException exception) =>
        Failure(
            UpdateErrorCode.DownloadCanceled,
            "SHA-256 verification was canceled.",
            exception);

    private static UpdateResult<string> Failure(
        UpdateErrorCode code,
        string message,
        Exception? exception = null) =>
        UpdateResult<string>.Failure(new UpdateError(code, message, exception));
}
