using System.Collections.ObjectModel;

namespace UpdateKit;

/// <summary>Configures GitHub release retrieval for an <see cref="UpdateClient"/>.</summary>
public sealed class UpdateClientOptions
{
    /// <summary>Gets the default HTTP user-agent value.</summary>
    public const string DefaultUserAgent = "UpdateKit";

    /// <summary>Gets the default timeout applied to each GitHub API request.</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets the GitHub repository owner.</summary>
    public string RepositoryOwner { get; init; } = string.Empty;

    /// <summary>Gets the GitHub repository name.</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>Gets an optional bearer token used for GitHub API release requests.</summary>
    public string? AccessToken { get; init; }

    /// <summary>Gets whether published prerelease versions are eligible.</summary>
    public bool IncludePrereleases { get; init; }

    /// <summary>Gets the user-agent sent with GitHub API release requests.</summary>
    public string UserAgent { get; init; } = DefaultUserAgent;

    /// <summary>Gets the timeout applied independently to each GitHub API page request.</summary>
    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>Returns all configuration validation messages without throwing.</summary>
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

    /// <summary>Throws <see cref="UpdateConfigurationException"/> when any option is invalid.</summary>
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
