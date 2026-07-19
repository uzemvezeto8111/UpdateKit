using System.Net;
using System.Text;
using UpdateKit.Core.Tests.Http;
using UpdateKit.Core.Tests.IO;

namespace UpdateKit.Core.Tests.Downloading;

public sealed class AssetDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_StreamsContentAndReturnsDownloadResult()
    {
        byte[] payload = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var progress = new RecordingProgress();
        var asset = CreateAsset(payload.LongLength);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(asset.DownloadUrl, request.RequestUri);
            return Task.FromResult(ResponseWithBytes(payload));
        });

        var result = await DownloadAsync(
            handler,
            destinationPath,
            asset,
            progress,
            bufferSize: 4);

        Assert.True(result.IsSuccess);
        Assert.Same(asset, result.Value.Asset);
        Assert.Equal(Path.GetFullPath(destinationPath), result.Value.FilePath);
        Assert.Equal(payload.LongLength, result.Value.BytesDownloaded);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(0, progress.Updates[0].BytesDownloaded);
        Assert.Equal(payload.LongLength, progress.Updates[^1].BytesDownloaded);
        Assert.Equal(payload.LongLength, progress.Updates[^1].TotalBytes);
        Assert.Equal(100d, progress.Updates[^1].Percentage);
        Assert.Contains(progress.Updates, update => update.BytesDownloaded == 4);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_ReportsUnknownTotalWhenContentLengthIsAbsent()
    {
        var payload = Encoding.UTF8.GetBytes("unknown length payload");
        using var directory = new TemporaryDirectory();
        var progress = new RecordingProgress();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(ResponseWithBytes(payload, includeContentLength: false)));

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            CreateAsset(payload.LongLength),
            progress);

        Assert.True(result.IsSuccess);
        Assert.All(progress.Updates, update => Assert.Null(update.TotalBytes));
        Assert.Equal(payload.LongLength, progress.Updates[^1].BytesDownloaded);
        Assert.Null(progress.Updates[^1].Percentage);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_SafelyReplacesExistingDestinationAfterSuccess()
    {
        var payload = Encoding.UTF8.GetBytes("new version");
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old version");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(ResponseWithBytes(payload)));

        var result = await DownloadAsync(handler, destinationPath, CreateAsset(payload.LongLength));

        Assert.True(result.IsSuccess);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PreservesExistingDestinationAfterHttpFailure()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old version");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var result = await DownloadAsync(handler, destinationPath);

        AssertDownloadFailed(result);
        Assert.Equal("old version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_MapsNetworkFailureAndDoesNotCreateDestination()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        var exception = new HttpRequestException("Network unavailable.");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(exception));

        var result = await DownloadAsync(handler, destinationPath);

        AssertDownloadFailed(result);
        Assert.Same(exception, result.Error.Exception);
        Assert.False(File.Exists(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_CleansPartialFileAndPreservesDestinationAfterStreamFailure()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old version");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(
                new ThrowAfterBytesStream(Encoding.UTF8.GetBytes("partial"))),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));

        var result = await DownloadAsync(handler, destinationPath);

        AssertDownloadFailed(result);
        Assert.IsType<IOException>(result.Error.Exception);
        Assert.Equal("old version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsCanceledAndCleansPartialFile()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old version");
        var blockingStream = new BlockingReadStream();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(blockingStream),
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        using var cancellationSource = new CancellationTokenSource();

        var downloadTask = DownloadAsync(
            handler,
            destinationPath,
            cancellationToken: cancellationSource.Token);
        await blockingStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        var result = await downloadTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.DownloadCanceled, result.Error.Code);
        Assert.IsAssignableFrom<OperationCanceledException>(result.Error.Exception);
        Assert.Equal("old version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PreCanceledRequestDoesNotCallHttpHandler()
    {
        using var directory = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(ResponseWithBytes([1, 2, 3])));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            cancellationToken: cancellationSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.DownloadCanceled, result.Error.Code);
        Assert.Equal(0, handler.CallCount);
        AssertNoTemporaryFiles(directory);
    }

    [Fact]
    public async Task DownloadAsync_PreservesDestinationAndCleansTemporaryFileWhenCommitFails()
    {
        var payload = Encoding.UTF8.GetBytes("new version");
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("UpdateKit.zip");
        await File.WriteAllTextAsync(destinationPath, "old version");
        var expected = new IOException("Replacement failed.");
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(ResponseWithBytes(payload)));
        Action<string, string> failingCommit = (temporaryPath, destination) =>
        {
            Assert.True(File.Exists(temporaryPath));
            Assert.Equal(destinationPath, destination);
            throw expected;
        };

        var result = await DownloadAsync(
            handler,
            destinationPath,
            commitFile: failingCommit);

        AssertDownloadFailed(result);
        Assert.Same(expected, result.Error.Exception);
        Assert.Equal("old version", await File.ReadAllTextAsync(destinationPath));
        AssertNoTemporaryFiles(directory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative-file.zip")]
    public async Task DownloadAsync_RejectsMissingOrRelativeDestination(string? destinationPath)
    {
        var handler = HandlerThatMustNotRun();

        var result = await DownloadAsync(handler, destinationPath);

        AssertInvalidConfiguration(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RejectsDirectoryAsDestination()
    {
        using var directory = new TemporaryDirectory();
        var handler = HandlerThatMustNotRun();

        var result = await DownloadAsync(handler, directory.Path);

        AssertInvalidConfiguration(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RejectsDestinationWithMissingParentDirectory()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = Path.Combine(directory.Path, "missing", "UpdateKit.zip");
        var handler = HandlerThatMustNotRun();

        var result = await DownloadAsync(handler, destinationPath);

        AssertInvalidConfiguration(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RejectsDestinationWithInvalidFileName()
    {
        using var directory = new TemporaryDirectory();
        var destinationPath = directory.GetPath("invalid\0name.zip");
        var handler = HandlerThatMustNotRun();

        var result = await DownloadAsync(handler, destinationPath);

        AssertInvalidConfiguration(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RejectsUnsupportedDownloadUrlWithoutHttpRequest()
    {
        using var directory = new TemporaryDirectory();
        var handler = HandlerThatMustNotRun();
        var asset = new ReleaseAsset(
            "UpdateKit.zip",
            new Uri("file:///C:/UpdateKit.zip"),
            10);

        var result = await DownloadAsync(
            handler,
            directory.GetPath("UpdateKit.zip"),
            asset);

        AssertInvalidConfiguration(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DownloadAsync_RejectsNullAsset()
    {
        using var directory = new TemporaryDirectory();
        var handler = HandlerThatMustNotRun();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var downloader = new AssetDownloader(httpClient);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => downloader.DownloadAsync(null!, directory.GetPath("UpdateKit.zip")));
        Assert.Equal(0, handler.CallCount);
    }

    private static async Task<UpdateResult<DownloadResult>> DownloadAsync(
        HttpMessageHandler handler,
        string? destinationPath,
        ReleaseAsset? asset = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        int bufferSize = 4,
        Action<string, string>? commitFile = null)
    {
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var downloader = new AssetDownloader(httpClient, bufferSize, commitFile);
        return await downloader.DownloadAsync(
            asset ?? CreateAsset(),
            destinationPath,
            progress,
            cancellationToken);
    }

    private static StubHttpMessageHandler HandlerThatMustNotRun() =>
        new((_, _) => throw new InvalidOperationException("The HTTP handler must not be called."));

    private static HttpResponseMessage ResponseWithBytes(
        byte[] bytes,
        bool includeContentLength = true)
    {
        var content = new StreamContent(new NonSeekableReadStream(bytes));
        if (includeContentLength)
        {
            content.Headers.ContentLength = bytes.LongLength;
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    private static ReleaseAsset CreateAsset(long size = 10) =>
        new(
            "UpdateKit.zip",
            new Uri("https://example.test/releases/UpdateKit.zip"),
            size,
            "application/zip");

    private static void AssertDownloadFailed(UpdateResult<DownloadResult> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.DownloadFailed, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static void AssertInvalidConfiguration(UpdateResult<DownloadResult> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.InvalidConfiguration, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static void AssertNoTemporaryFiles(TemporaryDirectory directory) =>
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".updatekit-*.tmp"));

    private sealed class RecordingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Updates { get; } = [];

        public void Report(DownloadProgress value) => Updates.Add(value);
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _stream;

        public NonSeekableReadStream(byte[] bytes)
        {
            _stream = new MemoryStream(bytes, writable: false);
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

        public override int Read(byte[] buffer, int offset, int count) =>
            _stream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowAfterBytesStream : Stream
    {
        private readonly byte[] _bytes;
        private bool _returnedBytes;

        public ThrowAfterBytesStream(byte[] bytes)
        {
            _bytes = bytes;
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_returnedBytes)
            {
                throw new IOException("The response stream failed.");
            }

            var bytesToCopy = Math.Min(count, _bytes.Length);
            _bytes.AsSpan(0, bytesToCopy).CopyTo(buffer.AsSpan(offset, bytesToCopy));
            _returnedBytes = true;
            return bytesToCopy;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_returnedBytes)
            {
                return ValueTask.FromException<int>(
                    new IOException("The response stream failed."));
            }

            var bytesToCopy = Math.Min(buffer.Length, _bytes.Length);
            _bytes.AsMemory(0, bytesToCopy).CopyTo(buffer);
            _returnedBytes = true;
            return ValueTask.FromResult(bytesToCopy);
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

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
