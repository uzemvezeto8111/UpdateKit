using System.IO;

namespace UpdateKit.Desktop.Internal;

internal static class ReleaseAssetDestinationResolver
{
    public static UpdateResult<string> Resolve(
        string? destinationPath,
        ReleaseAsset selectedAsset)
    {
        ArgumentNullException.ThrowIfNull(selectedAsset);

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return Failure("A destination file path or existing directory is required.");
        }

        if (!Path.IsPathFullyQualified(destinationPath))
        {
            return Failure("The destination path must be fully qualified.");
        }

        try
        {
            var fullPath = Path.GetFullPath(destinationPath);
            if (Directory.Exists(fullPath))
            {
                if (!IsValidFileName(selectedAsset.Name))
                {
                    return Failure(
                        "The selected release asset does not have a valid destination filename.");
                }

                return UpdateResult<string>.Success(Path.Combine(fullPath, selectedAsset.Name));
            }

            if (Path.EndsInDirectorySeparator(fullPath))
            {
                return Failure("The destination directory does not exist.");
            }

            var fileName = Path.GetFileName(fullPath);
            if (!IsValidFileName(fileName))
            {
                return Failure("The destination file name is invalid.");
            }

            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (parentDirectory is null || !Directory.Exists(parentDirectory))
            {
                return Failure("The destination directory does not exist.");
            }

            return UpdateResult<string>.Success(fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return Failure("The destination path is invalid.", exception);
        }
    }

    private static bool IsValidFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        !fileName.Contains('/') &&
        !fileName.Contains('\\') &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        fileName[^1] is not ('.' or ' ');

    private static UpdateResult<string> Failure(string message, Exception? exception = null) =>
        UpdateResult<string>.Failure(
            new UpdateError(UpdateErrorCode.InvalidConfiguration, message, exception));
}
