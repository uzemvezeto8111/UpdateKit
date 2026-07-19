namespace UpdateKit;

/// <summary>Represents either a successful non-null value or an operational <see cref="UpdateError"/>.</summary>
/// <typeparam name="T">The successful value type.</typeparam>
public sealed class UpdateResult<T>
    where T : notnull
{
    private readonly T? _value;
    private readonly UpdateError? _error;

    private UpdateResult(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private UpdateResult(UpdateError error)
    {
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the successful value, or throws when the result failed.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not contain a value.");

    /// <summary>Gets the operational error, or throws when the result succeeded.</summary>
    public UpdateError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("A successful result does not contain an error.");

    /// <summary>Creates a successful result containing a non-null value.</summary>
    public static UpdateResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new UpdateResult<T>(value);
    }

    /// <summary>Creates a failed result containing an error.</summary>
    public static UpdateResult<T> Failure(UpdateError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UpdateResult<T>(error);
    }
}
