using System.Net;
using System.Text;
using UpdateKit.Core.Tests.Http;
using UpdateKit.Core.Tests.IO;

namespace UpdateKit.Core.Tests.Downloading;

public sealed class DownloadRetryTests
{
    [Fact]
    public async Task DownloadAsync_DefaultConfigurationDoesNotRetry()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await DownloadAsync(handler, directory.GetPath("UpdateKit.zip"));

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task DownloadAsync_RetriesTransientHttpStatuses(HttpStatusCode statusCode)
    {
        using var directory = new TemporaryDirectory();
        var payload = Encoding.UTF8.GetBytes("complete payload");
        var responseNumber = 0;
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            handlerCallResponse(++responseNumber)));
        var delay = new RecordingDelay();

        HttpResponseMessage handlerCallResponse(int number) =>
            number == 1
                ? new HttpResponseMessage(statusCode)
                : BytesResponse(payload);

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 1),
            delay);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(delay.Delays, TimeSpan.FromMilliseconds(100));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task DownloadAsync_DoesNotRetryPermanentHttpStatuses(HttpStatusCode statusCode)
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 3),
            delay);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task DownloadAsync_RetriesTransportException()
    {
        using var directory = new TemporaryDirectory();
        var expected = new HttpRequestException("Connection reset.");
        var responseNumber = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
            ++responseNumber == 1
                ? Task.FromException<HttpResponseMessage>(expected)
                : Task.FromResult(BytesResponse([1, 2, 3])));
        var reports = new RecordingRetryProgress();
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 1, progress: reports),
            delay);

        Assert.True(result.IsSuccess);
        var report = Assert.Single(reports.Attempts);
        Assert.Equal(1, report.RetryNumber);
        Assert.Equal(1, report.MaximumRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(100), report.Delay);
        Assert.Null(report.StatusCode);
        Assert.Same(expected, report.Exception);
    }

    [Fact]
    public async Task DownloadAsync_RetriesNetworkIOExceptionFromRequestBoundary()
    {
        using var directory = new TemporaryDirectory();
        var expected = new IOException("Socket read failed.");
        var responseNumber = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
            ++responseNumber == 1
                ? Task.FromException<HttpResponseMessage>(expected)
                : Task.FromResult(BytesResponse([1, 2, 3])));
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 1),
            delay);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(delay.Delays);
    }

    [Fact]
    public async Task DownloadAsync_DoesNotRetryStatusCodedPermanentHttpRequestException()
    {
        using var directory = new TemporaryDirectory();
        var exception = new HttpRequestException(
            "Not found.",
            inner: null,
            HttpStatusCode.NotFound);
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(exception));
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 2),
            delay);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task DownloadAsync_DoesNotRetryInvalidRedirect()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("ftp://downloads.example.test/UpdateKit.zip");
            return Task.FromResult(response);
        });
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 3),
            delay);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task DownloadAsync_RetriesResponseReadFailureFromBeginningAndPreservesDestination()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old payload");
        var replacement = Encoding.UTF8.GetBytes("replacement payload");
        var responseNumber = 0;
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            ++responseNumber == 1
                ? StreamResponse(new ThrowAfterBytesStream([1, 2, 3]))
                : BytesResponse(replacement)));

        var result = await DownloadAsync(
            handler,
            destinationPath,
            RetryOptions(maxRetryAttempts: 1),
            new RecordingDelay());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(replacement, await File.ReadAllBytesAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_ExhaustsConfiguredRetriesWithBoundedExponentialDelays()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var reports = new RecordingRetryProgress();
        var delay = new RecordingDelay();
        var options = new DownloadRetryOptions
        {
            MaxRetryAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaximumDelay = TimeSpan.FromMilliseconds(250),
            RetryProgress = reports,
        };

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            options,
            delay);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Equal(4, handler.CallCount);
        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(250),
            ],
            delay.Delays);
        Assert.Equal([1, 2, 3], reports.Attempts.Select(attempt => attempt.RetryNumber));
        Assert.All(reports.Attempts, attempt =>
        {
            Assert.Equal(HttpStatusCode.BadGateway, attempt.StatusCode);
            Assert.Null(attempt.Exception);
        });
    }

    [Fact]
    public async Task DownloadAsync_AppliesDeterministicBoundedJitter()
    {
        using var directory = new TemporaryDirectory();
        var responseNumber = 0;
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            ++responseNumber == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : BytesResponse([1])));
        var delay = new RecordingDelay();
        var options = new DownloadRetryOptions
        {
            MaxRetryAttempts = 1,
            InitialDelay = TimeSpan.FromMilliseconds(200),
            MaximumDelay = TimeSpan.FromMilliseconds(250),
            JitterFactor = 0.5,
        };

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            options,
            delay,
            jitterSource: () => 1d);

        Assert.True(result.IsSuccess);
        Assert.Single(delay.Delays, TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task DownloadAsync_CancellationDuringBackoffStopsWithoutAnotherAttempt()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var delay = new BlockingDelay();
        using var cancellationSource = new CancellationTokenSource();

        var task = DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 3),
            delay,
            cancellationToken: cancellationSource.Token);
        await delay.Started.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await task;

        AssertError(result, UpdateErrorCode.DownloadCanceled);
        Assert.Equal(1, handler.CallCount);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_CancellationDuringRequestIsNotRetried()
    {
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return BytesResponse([1]);
        });
        var delay = new RecordingDelay();
        using var cancellationSource = new CancellationTokenSource();

        var task = DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            RetryOptions(maxRetryAttempts: 3),
            delay,
            cancellationToken: cancellationSource.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await task;

        AssertError(result, UpdateErrorCode.DownloadCanceled);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_InvalidDestinationIsNotRetried()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => throw new InvalidOperationException("HTTP must not run."));
        var delay = new RecordingDelay();

        var result = await DownloadAsync(
            handler,
            "relative.zip",
            RetryOptions(maxRetryAttempts: 3),
            delay);

        AssertError(result, UpdateErrorCode.InvalidConfiguration);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task DownloadAsync_DoesNotRetryFilesystemCommitFailure()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old payload");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse([1, 2, 3])));
        var delay = new RecordingDelay();
        var expected = new IOException("Commit failed.");
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var downloader = new AssetDownloader(
            httpClient,
            bufferSize: 4,
            (_, _) => throw expected,
            RetryOptions(maxRetryAttempts: 3),
            delay,
            () => 0.5d);

        var result = await downloader.DownloadAsync(CreateAsset(), destinationPath);

        AssertError(result, UpdateErrorCode.DownloadFailed);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
        Assert.Equal("old payload", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_ChecksumMismatchIsNotRetried()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse(Encoding.UTF8.GetBytes("payload"))));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var client = new UpdateClient(httpClient, new UpdateClientOptions
        {
            RepositoryOwner = "owner",
            RepositoryName = "repository",
            DownloadRetry = RetryOptions(maxRetryAttempts: 3),
        });
        var destinationPath = directory.GetPath("UpdateKit.zip");

        var result = await client.DownloadAndVerifyAsync(
            CreateAsset(),
            destinationPath,
            new string('0', 64));

        AssertError(result, UpdateErrorCode.ChecksumMismatch);
        Assert.Equal(1, handler.CallCount);
        Assert.False(File.Exists(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public void Constructor_RejectsInvalidRetryConfiguration()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse([1]))));
        var options = new DownloadRetryOptions { MaxRetryAttempts = -1 };

        var exception = Assert.Throws<UpdateConfigurationException>(
            () => new AssetDownloader(httpClient, options));

        Assert.Contains(
            exception.ValidationErrors,
            error => error.StartsWith(nameof(DownloadRetryOptions.MaxRetryAttempts), StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_RejectsNullRetryConfiguration()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => Task.FromResult(BytesResponse([1]))));

        Assert.Throws<ArgumentNullException>(
            () => new AssetDownloader(httpClient, retryOptions: null!));
    }

    private static async Task<UpdateResult<DownloadResult>> DownloadAsync(
        HttpMessageHandler handler,
        string destinationPath,
        DownloadRetryOptions? retryOptions = null,
        IDownloadDelay? delay = null,
        Func<double>? jitterSource = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var downloader = new AssetDownloader(
            new ReleaseAssetRequestClient(httpClient),
            bufferSize: 4,
            retryOptions: retryOptions,
            delay: delay,
            jitterSource: jitterSource);
        return await downloader.DownloadAsync(
            CreateAsset(),
            destinationPath,
            cancellationToken: cancellationToken);
    }

    private static DownloadRetryOptions RetryOptions(
        int maxRetryAttempts,
        IProgress<DownloadRetryAttempt>? progress = null) =>
        new()
        {
            MaxRetryAttempts = maxRetryAttempts,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaximumDelay = TimeSpan.FromMilliseconds(500),
            RetryProgress = progress,
        };

    private static HttpResponseMessage BytesResponse(byte[] payload) =>
        StreamResponse(new MemoryStream(payload, writable: false));

    private static HttpResponseMessage StreamResponse(Stream stream) =>
        new(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
        };

    private static ReleaseAsset CreateAsset() =>
        new(
            "UpdateKit.zip",
            new Uri("https://downloads.example.test/UpdateKit.zip"),
            7,
            "application/zip");

    private static void AssertError(
        UpdateResult<DownloadResult> result,
        UpdateErrorCode expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    private static void AssertNoTemporaryFiles(TemporaryDirectory directory) =>
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".updatekit-*.tmp"));

    private sealed class RecordingDelay : IDownloadDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingDelay : IDownloadDelay
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingRetryProgress : IProgress<DownloadRetryAttempt>
    {
        public List<DownloadRetryAttempt> Attempts { get; } = [];

        public void Report(DownloadRetryAttempt value) => Attempts.Add(value);
    }

    private sealed class ThrowAfterBytesStream : Stream
    {
        private readonly byte[] _bytes;
        private bool _returnedBytes;

        public ThrowAfterBytesStream(byte[] bytes) => _bytes = bytes;

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

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_returnedBytes)
            {
                return ValueTask.FromException<int>(new IOException("Connection reset."));
            }

            _returnedBytes = true;
            var count = Math.Min(buffer.Length, _bytes.Length);
            _bytes.AsMemory(0, count).CopyTo(buffer);
            return ValueTask.FromResult(count);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
