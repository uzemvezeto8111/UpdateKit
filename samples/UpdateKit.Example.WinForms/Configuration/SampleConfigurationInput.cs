namespace UpdateKit.Example.WinForms.Configuration;

internal enum SampleAssetSelectionMode
{
    ExactName,
    Extension,
}

internal enum SampleVerificationMode
{
    None,
    DirectSha256,
    ChecksumFile,
}

internal sealed record SampleConfigurationInput(
    string? RepositoryOwner,
    string? RepositoryName,
    string? AccessToken,
    string? CurrentVersion,
    bool IncludePrereleases,
    SampleAssetSelectionMode AssetSelectionMode,
    string? AssetSelectionValue,
    string? DestinationFilePath,
    SampleVerificationMode VerificationMode,
    string? VerificationValue,
    int MaximumRetryAttempts = 0,
    int RetryDelayMilliseconds = 1_000,
    UpdateKit.WinForms.ApplicationTheme? DialogTheme = null,
    bool ConfirmBeforeDownload = false);
