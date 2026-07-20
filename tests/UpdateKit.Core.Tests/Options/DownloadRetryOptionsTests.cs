namespace UpdateKit.Core.Tests.Options;

public sealed class DownloadRetryOptionsTests
{
    [Fact]
    public void Defaults_PreserveSingleAttemptBehavior()
    {
        var options = new DownloadRetryOptions();

        Assert.Equal(0, options.MaxRetryAttempts);
        Assert.Equal(DownloadRetryOptions.DefaultInitialDelay, options.InitialDelay);
        Assert.Equal(DownloadRetryOptions.DefaultMaximumDelay, options.MaximumDelay);
        Assert.Equal(0d, options.JitterFactor);
        Assert.Null(options.RetryProgress);
        Assert.Empty(options.GetValidationErrors());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validation_RejectsUnsupportedRetryCounts(int retryCount)
    {
        var options = new DownloadRetryOptions { MaxRetryAttempts = retryCount };

        Assert.Contains(
            options.GetValidationErrors(),
            error => error.StartsWith(nameof(DownloadRetryOptions.MaxRetryAttempts), StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_RejectsNegativeOrExcessiveDelays()
    {
        var options = new DownloadRetryOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(-1),
            MaximumDelay = TimeSpan.FromMilliseconds((double)int.MaxValue + 1),
        };

        var errors = options.GetValidationErrors();

        Assert.Contains(errors, error => error.StartsWith(nameof(DownloadRetryOptions.InitialDelay), StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith(nameof(DownloadRetryOptions.MaximumDelay), StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_RejectsMaximumDelayBelowInitialDelay()
    {
        var options = new DownloadRetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(2),
            MaximumDelay = TimeSpan.FromSeconds(1),
        };

        Assert.Contains(
            options.GetValidationErrors(),
            error => error.StartsWith(nameof(DownloadRetryOptions.MaximumDelay), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validation_RejectsUnsupportedJitter(double jitter)
    {
        var options = new DownloadRetryOptions { JitterFactor = jitter };

        Assert.Contains(
            options.GetValidationErrors(),
            error => error.StartsWith(nameof(DownloadRetryOptions.JitterFactor), StringComparison.Ordinal));
    }

    [Fact]
    public void UpdateClientOptions_IncludeNestedRetryValidation()
    {
        var options = new UpdateClientOptions
        {
            RepositoryOwner = "owner",
            RepositoryName = "repository",
            DownloadRetry = new DownloadRetryOptions { MaxRetryAttempts = -1 },
        };

        Assert.Contains(
            options.GetValidationErrors(),
            error => error.StartsWith("DownloadRetry.MaxRetryAttempts", StringComparison.Ordinal));
    }
}
