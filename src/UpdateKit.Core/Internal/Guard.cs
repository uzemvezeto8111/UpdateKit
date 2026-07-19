namespace UpdateKit.Internal;

internal static class Guard
{
    public static string NotWhiteSpace(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    public static Uri AbsoluteUri(Uri? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (!value.IsAbsoluteUri)
        {
            throw new ArgumentException("The URI must be absolute.", parameterName);
        }

        return value;
    }
}
