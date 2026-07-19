namespace UpdateKit;

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

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not contain a value.");

    public UpdateError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("A successful result does not contain an error.");

    public static UpdateResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new UpdateResult<T>(value);
    }

    public static UpdateResult<T> Failure(UpdateError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UpdateResult<T>(error);
    }
}
