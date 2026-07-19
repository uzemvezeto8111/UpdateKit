namespace UpdateKit.Core.Tests.Models;

public sealed class DownloadProgressTests
{
    [Fact]
    public void Percentage_IsCalculatedFromKnownTotal()
    {
        var progress = new DownloadProgress(25, 100);

        Assert.Equal(25d, progress.Percentage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Percentage_IsUnknownWithoutPositiveTotal(long? totalBytes)
    {
        var progress = new DownloadProgress(0, totalBytes);

        Assert.Null(progress.Percentage);
    }

    [Fact]
    public void Percentage_IsCappedWhenServerReportsTooSmallTotal()
    {
        var progress = new DownloadProgress(101, 100);

        Assert.Equal(100d, progress.Percentage);
    }

    [Fact]
    public void Constructor_RejectsNegativeDownloadedBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadProgress(-1, 100));
    }

    [Fact]
    public void Constructor_RejectsNegativeTotalBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadProgress(0, -1));
    }
}
