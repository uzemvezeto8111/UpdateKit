using UpdateKit.Internal;

namespace UpdateKit;

public sealed record UpdateError
{
    public UpdateError(UpdateErrorCode code, string message, Exception? exception = null)
    {
        Code = code;
        Message = Guard.NotWhiteSpace(message, nameof(message));
        Exception = exception;
    }

    public UpdateErrorCode Code { get; }

    public string Message { get; }

    public Exception? Exception { get; }
}
