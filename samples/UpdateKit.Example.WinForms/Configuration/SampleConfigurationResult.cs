using System.Collections.ObjectModel;

namespace UpdateKit.Example.WinForms.Configuration;

internal sealed class SampleConfigurationResult
{
    private readonly SampleUpdateConfiguration? _configuration;

    private SampleConfigurationResult(
        SampleUpdateConfiguration? configuration,
        IReadOnlyList<string> errors)
    {
        _configuration = configuration;
        Errors = errors;
    }

    public bool IsValid => _configuration is not null;

    public SampleUpdateConfiguration Configuration => IsValid
        ? _configuration!
        : throw new InvalidOperationException("Invalid sample configuration has no value.");

    public IReadOnlyList<string> Errors { get; }

    public static SampleConfigurationResult Success(SampleUpdateConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new SampleConfigurationResult(configuration, Array.Empty<string>());
    }

    public static SampleConfigurationResult Failure(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var snapshot = new ReadOnlyCollection<string>(errors.ToArray());
        if (snapshot.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }

        return new SampleConfigurationResult(null, snapshot);
    }
}
