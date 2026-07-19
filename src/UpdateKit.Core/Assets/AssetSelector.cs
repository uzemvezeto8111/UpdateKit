namespace UpdateKit;

public static class AssetSelector
{
    public static UpdateResult<ReleaseAsset> ByExactName(
        ReleaseInfo release,
        string? assetName)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (!IsValidAssetName(assetName))
        {
            return InvalidCriteria(
                "An exact asset name is required and cannot contain control characters or path separators.");
        }

        return SelectFirst(
            release.Assets,
            asset => string.Equals(asset.Name, assetName, StringComparison.Ordinal),
            $"No release asset has the exact name '{assetName}'.");
    }

    public static UpdateResult<ReleaseAsset> ByExtension(
        ReleaseInfo release,
        string? extension)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (!TryNormalizeExtension(extension, out var normalizedExtension))
        {
            return InvalidCriteria(
                "An extension is required and must be a valid suffix with an optional leading dot.");
        }

        return SelectFirst(
            release.Assets,
            asset => asset.Name.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase),
            $"No release asset has the extension '{normalizedExtension}'.");
    }

    public static UpdateResult<ReleaseAsset> ByPredicate(
        ReleaseInfo release,
        Func<ReleaseAsset, bool>? predicate)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (predicate is null)
        {
            return InvalidCriteria("An asset-selection predicate is required.");
        }

        return SelectFirst(
            release.Assets,
            predicate,
            "No release asset matched the supplied predicate.");
    }

    private static UpdateResult<ReleaseAsset> SelectFirst(
        IEnumerable<ReleaseAsset> assets,
        Func<ReleaseAsset, bool> predicate,
        string noMatchMessage)
    {
        foreach (var asset in assets)
        {
            if (predicate(asset))
            {
                return UpdateResult<ReleaseAsset>.Success(asset);
            }
        }

        return UpdateResult<ReleaseAsset>.Failure(
            new UpdateError(UpdateErrorCode.AssetNotFound, noMatchMessage));
    }

    private static bool IsValidAssetName(string? assetName) =>
        !string.IsNullOrWhiteSpace(assetName) &&
        !assetName.Contains('/') &&
        !assetName.Contains('\\') &&
        !assetName.Any(char.IsControl);

    private static bool TryNormalizeExtension(
        string? extension,
        out string normalizedExtension)
    {
        normalizedExtension = string.Empty;

        if (string.IsNullOrWhiteSpace(extension) ||
            extension.Any(char.IsWhiteSpace) ||
            extension.Any(IsInvalidExtensionCharacter))
        {
            return false;
        }

        var suffix = extension[0] == '.' ? extension[1..] : extension;
        if (suffix.Length == 0 ||
            suffix[0] == '.' ||
            suffix[^1] == '.' ||
            suffix.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        normalizedExtension = $".{suffix}";
        return true;
    }

    private static bool IsInvalidExtensionCharacter(char character) =>
        character is '/' or '\\' or '*' or '?' or '"' or '<' or '>' or '|' or ':' ||
        char.IsControl(character);

    private static UpdateResult<ReleaseAsset> InvalidCriteria(string message) =>
        UpdateResult<ReleaseAsset>.Failure(
            new UpdateError(UpdateErrorCode.InvalidConfiguration, message));
}
