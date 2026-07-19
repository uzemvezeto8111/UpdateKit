using System.Collections.ObjectModel;

namespace UpdateKit;

/// <summary>Represents one or more invalid <see cref="UpdateClientOptions"/> values.</summary>
public sealed class UpdateConfigurationException : Exception
{
    internal UpdateConfigurationException(IReadOnlyCollection<string> validationErrors)
        : base(CreateMessage(validationErrors))
    {
        ValidationErrors = new ReadOnlyCollection<string>(validationErrors.ToArray());
    }

    /// <summary>Gets the immutable validation-message snapshot.</summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    private static string CreateMessage(IReadOnlyCollection<string> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(validationErrors));
        }

        return $"UpdateKit configuration is invalid: {string.Join(" ", validationErrors)}";
    }
}
