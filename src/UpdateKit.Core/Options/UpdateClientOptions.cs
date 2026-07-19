using System.Collections.ObjectModel;

namespace UpdateKit;

public sealed class UpdateClientOptions
{
    public const string DefaultUserAgent = "UpdateKit";

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    public string RepositoryOwner { get; init; } = string.Empty;

    public string RepositoryName { get; init; } = string.Empty;

    public string? AccessToken { get; init; }

    public bool IncludePrereleases { get; init; }

    public string UserAgent { get; init; } = DefaultUserAgent;

    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        ValidateRepositoryPart(RepositoryOwner, nameof(RepositoryOwner), errors);
        ValidateRepositoryPart(RepositoryName, nameof(RepositoryName), errors);

        if (string.IsNullOrWhiteSpace(UserAgent))
        {
            errors.Add($"{nameof(UserAgent)} is required.");
        }
        else if (ContainsNewLine(UserAgent))
        {
            errors.Add($"{nameof(UserAgent)} cannot contain a line break.");
        }

        if (AccessToken is not null)
        {
            if (string.IsNullOrWhiteSpace(AccessToken))
            {
                errors.Add($"{nameof(AccessToken)} cannot be empty or whitespace when provided.");
            }
            else if (AccessToken.Any(char.IsWhiteSpace))
            {
                errors.Add($"{nameof(AccessToken)} cannot contain whitespace.");
            }
        }

        if (RequestTimeout <= TimeSpan.Zero ||
            RequestTimeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            errors.Add(
                $"{nameof(RequestTimeout)} must be greater than zero and no greater than {int.MaxValue} milliseconds.");
        }

        return new ReadOnlyCollection<string>(errors);
    }

    public void Validate()
    {
        var errors = GetValidationErrors();

        if (errors.Count > 0)
        {
            throw new UpdateConfigurationException(errors);
        }
    }

    private static void ValidateRepositoryPart(
        string value,
        string propertyName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{propertyName} is required.");
        }
        else if (value.Contains('/') || value.Contains('\\'))
        {
            errors.Add($"{propertyName} cannot contain a path separator.");
        }
    }

    private static bool ContainsNewLine(string value) =>
        value.Contains('\r') || value.Contains('\n');
}
