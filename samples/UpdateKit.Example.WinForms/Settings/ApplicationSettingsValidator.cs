namespace UpdateKit.Example.WinForms.Settings;

internal static class ApplicationSettingsValidator
{
    public static IReadOnlyList<string> Validate(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();

        if (!Enum.IsDefined(settings.Theme))
        {
            errors.Add("Choose System, Light, or Dark as the application theme.");
        }

        if (!Enum.IsDefined(settings.AssetSelectionMode))
        {
            errors.Add("Choose a supported asset-selection mode.");
        }

        if (settings.MaximumRetryCount is < 0 or > ApplicationSettings.MaximumAllowedRetries)
        {
            errors.Add($"Maximum retry count must be between 0 and {ApplicationSettings.MaximumAllowedRetries}.");
        }

        if (settings.RetryDelayMilliseconds is < 0 or > ApplicationSettings.MaximumRetryDelayMilliseconds)
        {
            errors.Add("Retry delay must be between 0 and 300 seconds.");
        }

        if (!IsExistingAbsoluteDirectory(settings.DefaultDownloadDirectory))
        {
            errors.Add("Default download directory must be an existing absolute directory.");
        }

        return errors;
    }

    private static bool IsExistingAbsoluteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            return Directory.Exists(Path.GetFullPath(path));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
