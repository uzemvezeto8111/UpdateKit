using System.Net;
using System.Security.Cryptography;
using System.Text;
using UpdateKit.Core.Tests.Http;
using UpdateKit.Core.Tests.IO;

namespace UpdateKit.Core.Tests.Verification;

public sealed class Sha256VerifierTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifyAsync_AcceptsLowercaseAndUppercaseChecksums(bool uppercase)
    {
        var payload = Encoding.UTF8.GetBytes("verified payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var expected = Checksum(payload);
        expected = uppercase ? expected.ToUpperInvariant() : expected.ToLowerInvariant();
        var handler = HandlerThatMustNotRun();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyAsync(download, expected);

        Assert.True(result.IsSuccess);
        Assert.Same(download, result.Value);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_DeletesFileAndReturnsMismatch()
    {
        var payload = Encoding.UTF8.GetBytes("untrusted payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyAsync(download, new string('0', 64));

        AssertError(result, UpdateErrorCode.ChecksumMismatch);
        Assert.False(File.Exists(download.FilePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData(" 0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task VerifyAsync_RejectsInvalidDirectChecksumWithoutReadingFile(
        string? expectedChecksum)
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        Func<string, Stream> openFile = _ =>
            throw new InvalidOperationException("The file must not be opened.");
        var verifier = new Sha256Verifier(httpClient, openFile, File.Delete);

        var result = await verifier.VerifyAsync(download, expectedChecksum);

        AssertError(result, UpdateErrorCode.InvalidChecksum);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyAsync_MapsMissingFileToFileSystemError()
    {
        using var directory = new TemporaryDirectory();
        var download = CreateDownload(directory.GetPath("missing.zip"), 10);
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyAsync(download, new string('0', 64));

        AssertError(result, UpdateErrorCode.FileSystemError);
        Assert.IsType<FileNotFoundException>(result.Error.Exception);
    }

    [Fact]
    public async Task VerifyAsync_MapsFileReadFailureAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var expected = new IOException("Read failed.");
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        Func<string, Stream> openFile = _ => throw expected;
        var verifier = new Sha256Verifier(httpClient, openFile, File.Delete);

        var result = await verifier.VerifyAsync(download, Checksum(payload));

        AssertError(result, UpdateErrorCode.FileSystemError);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsCanceledBeforeOpeningAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        Func<string, Stream> openFile = _ =>
            throw new InvalidOperationException("The file must not be opened.");
        var verifier = new Sha256Verifier(httpClient, openFile, File.Delete);

        var result = await verifier.VerifyAsync(
            download,
            Checksum(payload),
            cancellationSource.Token);

        AssertCanceled(result);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsCanceledDuringHashingAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var blockingStream = new BlockingReadStream();
        using var cancellationSource = new CancellationTokenSource();
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var verifier = new Sha256Verifier(
            httpClient,
            _ => blockingStream,
            File.Delete);

        var verificationTask = verifier.VerifyAsync(
            download,
            Checksum(payload),
            cancellationSource.Token);
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await verificationTask;

        AssertCanceled(result);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyAsync_MapsDeletionFailureAndLeavesInvalidFile()
    {
        var payload = Encoding.UTF8.GetBytes("invalid payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var expected = new IOException("Delete failed.");
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var verifier = new Sha256Verifier(
            httpClient,
            path => File.OpenRead(path),
            _ => throw expected);

        var result = await verifier.VerifyAsync(download, new string('0', 64));

        AssertError(result, UpdateErrorCode.FileSystemError);
        Assert.Same(expected, result.Error.Exception);
        Assert.True(File.Exists(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_RetrievesParsesAndVerifiesAsset()
    {
        var payload = Encoding.UTF8.GetBytes("release payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(
            directory,
            payload,
            "UpdateKit portable package.zip");
        var checksumAsset = CreateChecksumAsset();
        var checksumContent =
            $"{new string('1', 64)}  other.zip\n{Checksum(payload)} *{download.Asset.Name}";
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(checksumAsset.DownloadUrl, request.RequestUri);
            return Task.FromResult(TextResponse(checksumContent));
        });
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            checksumAsset);

        Assert.True(result.IsSuccess);
        Assert.Same(download, result.Value);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_DeletesFileOnMismatch()
    {
        var payload = Encoding.UTF8.GetBytes("invalid release payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                TextResponse($"{new string('0', 64)}  {download.Asset.Name}")));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.ChecksumMismatch);
        Assert.False(File.Exists(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_ReturnsNotFoundAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                TextResponse($"{Checksum(payload)}  other.zip")));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.ChecksumNotFound);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n \t")]
    [InlineData("malformed checksum line")]
    public async Task VerifyFromChecksumFileAsync_MapsEmptyAndMalformedFiles(
        string checksumContent)
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(TextResponse(checksumContent)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        var expectedCode = string.IsNullOrWhiteSpace(checksumContent)
            ? UpdateErrorCode.ChecksumNotFound
            : UpdateErrorCode.InvalidChecksum;
        AssertError(result, expectedCode);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_MapsHttpFailureAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.NetworkError);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_MapsNetworkExceptionAndPreservesFile()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var expected = new HttpRequestException("Network failed.");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(expected));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.NetworkError);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_MapsResponseStreamFailure()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var expected = new IOException("Response failed.");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ThrowingReadStream(expected)),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.NetworkError);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_RejectsInvalidUtf8()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0xff, 0xfe, 0xfd]),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset());

        AssertError(result, UpdateErrorCode.InvalidChecksum);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_ReturnsCanceledDuringRetrieval()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var blockingStream = new BlockingReadStream();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(blockingStream),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        using var cancellationSource = new CancellationTokenSource();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var verificationTask = verifier.VerifyFromChecksumFileAsync(
            download,
            CreateChecksumAsset(),
            cancellationSource.Token);
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await verificationTask;

        AssertCanceled(result);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerifyFromChecksumFileAsync_RejectsUnsupportedUrlWithoutRequest()
    {
        var payload = Encoding.UTF8.GetBytes("preserved payload");
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, payload);
        var checksumAsset = new ReleaseAsset(
            "checksums.txt",
            new Uri("file:///C:/checksums.txt"),
            100,
            "text/plain");
        var handler = HandlerThatMustNotRun();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var verifier = new Sha256Verifier(httpClient);

        var result = await verifier.VerifyFromChecksumFileAsync(download, checksumAsset);

        AssertError(result, UpdateErrorCode.InvalidConfiguration);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(payload, await File.ReadAllBytesAsync(download.FilePath));
    }

    [Fact]
    public async Task VerificationMethods_RejectNullRequiredModels()
    {
        using var directory = new TemporaryDirectory();
        var download = await CreateDownloadAsync(directory, [1, 2, 3]);
        using var httpClient = new HttpClient(HandlerThatMustNotRun());
        var verifier = new Sha256Verifier(httpClient);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => verifier.VerifyAsync(null!, new string('0', 64)));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => verifier.VerifyFromChecksumFileAsync(null!, CreateChecksumAsset()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => verifier.VerifyFromChecksumFileAsync(download, null!));
    }

    private static async Task<DownloadResult> CreateDownloadAsync(
        TemporaryDirectory directory,
        byte[] payload,
        string assetName = "UpdateKit.zip")
    {
        var filePath = directory.GetPath("downloaded-file.bin");
        await File.WriteAllBytesAsync(filePath, payload);
        return CreateDownload(filePath, payload.LongLength, assetName);
    }

    private static DownloadResult CreateDownload(
        string filePath,
        long size,
        string assetName = "UpdateKit.zip")
    {
        var asset = new ReleaseAsset(
            assetName,
            new Uri($"https://example.test/releases/{Uri.EscapeDataString(assetName)}"),
            size,
            "application/octet-stream");
        return new DownloadResult(asset, filePath, size);
    }

    private static ReleaseAsset CreateChecksumAsset() =>
        new(
            "checksums.sha256",
            new Uri("https://example.test/releases/checksums.sha256"),
            100,
            "text/plain");

    private static string Checksum(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload));

    private static HttpResponseMessage TextResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain"),
        };

    private static StubHttpMessageHandler HandlerThatMustNotRun() =>
        new((_, _) => throw new InvalidOperationException("The HTTP handler must not be called."));

    private static void AssertError(
        UpdateResult<DownloadResult> result,
        UpdateErrorCode expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static void AssertCanceled(UpdateResult<DownloadResult> result)
    {
        AssertError(result, UpdateErrorCode.DownloadCanceled);
        Assert.IsAssignableFrom<OperationCanceledException>(result.Error.Exception);
    }

    private sealed class ThrowingReadStream : Stream
    {
        private readonly IOException _exception;

        public ThrowingReadStream(IOException exception)
        {
            _exception = exception;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw _exception;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(_exception);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
