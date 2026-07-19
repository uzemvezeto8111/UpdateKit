using UpdateKit.Internal;

namespace UpdateKit;

/// <summary>Describes an expected update-operation failure.</summary>
public sealed record UpdateError
{
    /// <summary>Creates an error with a stable category, message, and optional diagnostic exception.</summary>
    public UpdateError(UpdateErrorCode code, string message, Exception? exception = null)
    {
        Code = code;
        Message = Guard.NotWhiteSpace(message, nameof(message));
        Exception = exception;
    }

    /// <summary>Gets the stable error category.</summary>
    public UpdateErrorCode Code { get; }

    /// <summary>Gets the actionable error message.</summary>
    public string Message { get; }

    /// <summary>Gets the optional diagnostic exception associated with the failure.</summary>
    public Exception? Exception { get; }
}
