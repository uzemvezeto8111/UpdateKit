using System.Collections.ObjectModel;

namespace UpdateKit;

/// <summary>Configures transient download retries for an <see cref="UpdateClient"/> or <see cref="AssetDownloader"/>.</summary>
public sealed class DownloadRetryOptions
{
    /// <summary>Gets the default initial retry delay.</summary>
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(1);

    /// <summary>Gets the default maximum retry delay.</summary>
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum number of retries after the initial request. The default is zero, preserving
    /// single-attempt behavior.
    /// </summary>
    public int MaxRetryAttempts { get; init; }

    /// <summary>Gets the delay before the first retry.</summary>
    public TimeSpan InitialDelay { get; init; } = DefaultInitialDelay;

    /// <summary>Gets the upper bound applied to every retry delay.</summary>
    public TimeSpan MaximumDelay { get; init; } = DefaultMaximumDelay;

    /// <summary>
    /// Gets the symmetric jitter factor from zero through one. Zero disables jitter; for example,
    /// 0.2 randomizes each delay between 80% and 120% before applying <see cref="MaximumDelay"/>.
    /// </summary>
    public double JitterFactor { get; init; }

    /// <summary>Gets an optional progress sink notified immediately before each retry delay.</summary>
    public IProgress<DownloadRetryAttempt>? RetryProgress { get; init; }

    /// <summary>Returns all retry-configuration validation messages.</summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (MaxRetryAttempts is < 0 or > 100)
        {
            errors.Add($"{nameof(MaxRetryAttempts)} must be between 0 and 100.");
        }

        if (InitialDelay < TimeSpan.Zero ||
            InitialDelay > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            errors.Add(
                $"{nameof(InitialDelay)} must be non-negative and no greater than {int.MaxValue} milliseconds.");
        }

        if (MaximumDelay < TimeSpan.Zero ||
            MaximumDelay > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            errors.Add(
                $"{nameof(MaximumDelay)} must be non-negative and no greater than {int.MaxValue} milliseconds.");
        }
        else if (MaximumDelay < InitialDelay)
        {
            errors.Add($"{nameof(MaximumDelay)} cannot be less than {nameof(InitialDelay)}.");
        }

        if (!double.IsFinite(JitterFactor) || JitterFactor is < 0d or > 1d)
        {
            errors.Add($"{nameof(JitterFactor)} must be a finite value between 0 and 1.");
        }

        return new ReadOnlyCollection<string>(errors);
    }

    internal void Validate()
    {
        var errors = GetValidationErrors();
        if (errors.Count > 0)
        {
            throw new UpdateConfigurationException(errors);
        }
    }
}
