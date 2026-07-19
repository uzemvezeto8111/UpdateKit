namespace UpdateKit;

internal static class ChecksumFileParser
{
    public static UpdateResult<byte[]> FindSha256(
        string content,
        string targetFileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFileName);

        var checksums = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var reader = new StringReader(content);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (lineNumber == 1 && line.Length > 0 && line[0] == '\uFEFF')
            {
                line = line[1..];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryParseLine(line, out var fileName, out var checksum))
            {
                return InvalidChecksum(
                    $"Checksum-file line {lineNumber} is not a valid SHA-256 entry.");
            }

            if (checksums.TryGetValue(fileName, out var existingChecksum))
            {
                if (!Sha256Checksum.Equals(existingChecksum, checksum))
                {
                    return InvalidChecksum(
                        $"Checksum file contains conflicting entries for '{fileName}'.");
                }

                continue;
            }

            checksums.Add(fileName, checksum);
        }

        return checksums.TryGetValue(targetFileName, out var matchingChecksum)
            ? UpdateResult<byte[]>.Success(matchingChecksum)
            : UpdateResult<byte[]>.Failure(
                new UpdateError(
                    UpdateErrorCode.ChecksumNotFound,
                    $"No SHA-256 checksum entry exactly matches '{targetFileName}'."));
    }

    private static bool TryParseLine(
        string line,
        out string fileName,
        out byte[] checksum)
    {
        fileName = string.Empty;
        checksum = [];

        if (line.Length <= Sha256Checksum.HexLength ||
            !Sha256Checksum.TryParse(
                line[..Sha256Checksum.HexLength],
                out checksum))
        {
            return false;
        }

        var index = Sha256Checksum.HexLength;
        if (!IsDelimiter(line[index]))
        {
            return false;
        }

        while (index < line.Length && IsDelimiter(line[index]))
        {
            index++;
        }

        if (index < line.Length && line[index] == '*')
        {
            index++;
        }

        if (index >= line.Length)
        {
            return false;
        }

        fileName = line[index..];
        return fileName.Length > 0 && !fileName.Any(char.IsControl);
    }

    private static bool IsDelimiter(char value) => value is ' ' or '\t';

    private static UpdateResult<byte[]> InvalidChecksum(string message) =>
        UpdateResult<byte[]>.Failure(
            new UpdateError(UpdateErrorCode.InvalidChecksum, message));
}
