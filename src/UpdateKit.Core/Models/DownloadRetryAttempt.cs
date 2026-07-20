using System.Net;

namespace UpdateKit;

/// <summary>Describes a transient failure and the next bounded download retry.</summary>
public sealed record DownloadRetryAttempt
{
    /// <summary>Creates a retry notification.</summary>
    public DownloadRetryAttempt(
        int retryNumber,
        int maximumRetryAttempts,
        TimeSpan delay,
        HttpStatusCode? statusCode,
        Exception? exception)
    {
        if (retryNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryNumber));
        }

        if (maximumRetryAttempts < retryNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetryAttempts));
        }

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        RetryNumber = retryNumber;
        MaximumRetryAttempts = maximumRetryAttempts;
        Delay = delay;
        StatusCode = statusCode;
        Exception = exception;
    }

    /// <summary>Gets the one-based retry number; one identifies the second HTTP attempt.</summary>
    public int RetryNumber { get; }

    /// <summary>Gets the configured maximum number of retries after the initial request.</summary>
    public int MaximumRetryAttempts { get; }

    /// <summary>Gets the bounded delay before the next attempt.</summary>
    public TimeSpan Delay { get; }

    /// <summary>Gets the transient HTTP status, or <see langword="null"/> for a network exception.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the transient network exception, when one caused the retry.</summary>
    public Exception? Exception { get; }
}
