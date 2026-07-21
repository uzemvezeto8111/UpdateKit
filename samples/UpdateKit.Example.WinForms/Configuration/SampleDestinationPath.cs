namespace UpdateKit.Example.WinForms.Configuration;

internal static class SampleDestinationPath
{
    public static bool TryNormalize(
        string? value,
        out string normalizedPath,
        out string? validationError)
    {
        normalizedPath = string.Empty;
        validationError = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            validationError = "A destination file path or existing directory is required.";
            return false;
        }

        var trimmed = value.Trim();
        if (!Path.IsPathFullyQualified(trimmed))
        {
            validationError = "The destination path must be fully qualified.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            if (Directory.Exists(fullPath))
            {
                normalizedPath = fullPath;
                return true;
            }

            if (Path.EndsInDirectorySeparator(fullPath))
            {
                validationError = "The destination directory does not exist.";
                return false;
            }

            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName[^1] is '.' or ' ')
            {
                validationError = "The destination file name is invalid.";
                return false;
            }

            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (parentDirectory is null || !Directory.Exists(parentDirectory))
            {
                validationError = "The destination directory does not exist.";
                return false;
            }

            normalizedPath = fullPath;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            validationError = "The destination path is invalid.";
            return false;
        }
    }
}
