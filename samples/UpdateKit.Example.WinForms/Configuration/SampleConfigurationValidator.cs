namespace UpdateKit.Example.WinForms.Configuration;

internal static class SampleConfigurationValidator
{
    private static readonly ReleaseInfo EmptyValidationRelease =
        new(
            1,
            "1.0.0",
            "Validation release",
            null,
            new Uri("https://example.invalid/releases/1"),
            DateTimeOffset.UnixEpoch,
            false,
            false,
            []);

    public static SampleConfigurationResult Validate(SampleConfigurationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var repositoryOwner = input.RepositoryOwner?.Trim() ?? string.Empty;
        var repositoryName = input.RepositoryName?.Trim() ?? string.Empty;
        var accessToken = string.IsNullOrWhiteSpace(input.AccessToken)
            ? null
            : input.AccessToken;
        var currentVersion = input.CurrentVersion ?? string.Empty;
        var assetSelectionValue = input.AssetSelectionValue ?? string.Empty;
        var destinationFilePath = input.DestinationFilePath?.Trim() ?? string.Empty;
        var verificationValue = input.VerificationValue;
        var errors = new List<string>();

        var clientOptions = new UpdateClientOptions
        {
            RepositoryOwner = repositoryOwner,
            RepositoryName = repositoryName,
            AccessToken = accessToken,
            IncludePrereleases = input.IncludePrereleases,
            UserAgent = "UpdateKit.Example.WinForms",
            DownloadRetry = new DownloadRetryOptions
            {
                MaxRetryAttempts = input.MaximumRetryAttempts,
                InitialDelay = TimeSpan.FromMilliseconds(input.RetryDelayMilliseconds),
                MaximumDelay = TimeSpan.FromMilliseconds(Math.Max(
                    input.RetryDelayMilliseconds,
                    (int)DownloadRetryOptions.DefaultMaximumDelay.TotalMilliseconds)),
            },
        };
        errors.AddRange(clientOptions.GetValidationErrors());

        var versionResult = SemanticVersion.ParseTag(currentVersion);
        if (!versionResult.IsSuccess)
        {
            errors.Add($"Current version: {versionResult.Error.Message}");
        }

        ValidateAssetSelection(
            input.AssetSelectionMode,
            assetSelectionValue,
            errors);
        if (!SampleDestinationPath.TryNormalize(
                destinationFilePath,
                out var normalizedDestinationPath,
                out var destinationError))
        {
            errors.Add(destinationError!);
        }
        ValidateVerification(
            input.VerificationMode,
            verificationValue,
            errors);

        if (input.DialogTheme is { } theme && !Enum.IsDefined(theme))
        {
            errors.Add("Choose a supported dialog theme.");
        }

        if (errors.Count > 0)
        {
            return SampleConfigurationResult.Failure(errors);
        }

        return SampleConfigurationResult.Success(
            new SampleUpdateConfiguration(
                repositoryOwner,
                repositoryName,
                accessToken,
                currentVersion,
                input.IncludePrereleases,
                input.AssetSelectionMode,
                assetSelectionValue,
                normalizedDestinationPath,
                input.VerificationMode,
                verificationValue,
                input.MaximumRetryAttempts,
                input.RetryDelayMilliseconds,
                input.DialogTheme,
                input.ConfirmBeforeDownload));
    }

    private static void ValidateAssetSelection(
        SampleAssetSelectionMode mode,
        string value,
        ICollection<string> errors)
    {
        if (!Enum.IsDefined(mode))
        {
            errors.Add("Choose a supported asset-selection mode.");
            return;
        }

        var result = mode == SampleAssetSelectionMode.ExactName
            ? AssetSelector.ByExactName(EmptyValidationRelease, value)
            : AssetSelector.ByExtension(EmptyValidationRelease, value);

        if (!result.IsSuccess && result.Error.Code == UpdateErrorCode.InvalidConfiguration)
        {
            errors.Add($"Asset selection: {result.Error.Message}");
        }
    }

    private static void ValidateVerification(
        SampleVerificationMode mode,
        string? value,
        ICollection<string> errors)
    {
        if (!Enum.IsDefined(mode))
        {
            errors.Add("Choose a supported checksum-verification mode.");
            return;
        }

        if (mode == SampleVerificationMode.None)
        {
            return;
        }

        if (mode == SampleVerificationMode.DirectSha256)
        {
            if (!IsSha256(value))
            {
                errors.Add("The expected SHA-256 checksum must contain exactly 64 hexadecimal characters.");
            }

            return;
        }

        var result = AssetSelector.ByExactName(EmptyValidationRelease, value);
        if (!result.IsSuccess && result.Error.Code == UpdateErrorCode.InvalidConfiguration)
        {
            errors.Add($"Checksum-file asset: {result.Error.Message}");
        }
    }

    private static bool IsSha256(string? value)
    {
        if (value?.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
