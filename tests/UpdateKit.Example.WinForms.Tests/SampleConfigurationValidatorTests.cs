using UpdateKit.Example.WinForms.Configuration;

namespace UpdateKit.Example.WinForms.Tests;

public sealed class SampleConfigurationValidatorTests
{
    [Fact]
    public void Validate_ValidExactNameConfiguration_NormalizesAndMapsClientOptions()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            RepositoryOwner = "  octocat  ",
            RepositoryName = "  hello-world ",
            AccessToken = "github-token",
            IncludePrereleases = true,
        };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("octocat", result.Configuration.RepositoryOwner);
        Assert.Equal("hello-world", result.Configuration.RepositoryName);
        Assert.Equal(Path.GetFullPath(input.DestinationFilePath!), result.Configuration.DestinationFilePath);

        var options = result.Configuration.CreateClientOptions();
        Assert.Equal("octocat", options.RepositoryOwner);
        Assert.Equal("hello-world", options.RepositoryName);
        Assert.Equal("github-token", options.AccessToken);
        Assert.True(options.IncludePrereleases);
        Assert.Equal("UpdateKit.Example.WinForms", options.UserAgent);
    }

    [Fact]
    public void Validate_BlankOptionalToken_MapsToNull()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with { AccessToken = "   " };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Null(result.Configuration.AccessToken);
        Assert.Null(result.Configuration.CreateClientOptions().AccessToken);
    }

    [Fact]
    public void Validate_RetryAndThemeSettings_MapToExistingPublicOptions()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            MaximumRetryAttempts = 4,
            RetryDelayMilliseconds = 2_500,
            DialogTheme = UpdateKit.WinForms.ApplicationTheme.Dark,
            ConfirmBeforeDownload = true,
        };

        var configuration = AssertValid(input);
        var clientOptions = configuration.CreateClientOptions();
        using var httpClient = CreateNetworkRejectingClient();
        var client = new UpdateClient(httpClient, clientOptions);
        var dialogOptions = configuration.CreateDialogOptions(client);

        Assert.Equal(4, clientOptions.DownloadRetry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(2_500), clientOptions.DownloadRetry.InitialDelay);
        Assert.Equal(UpdateKit.WinForms.ApplicationTheme.Dark, dialogOptions.Theme);
        Assert.True(dialogOptions.ConfirmBeforeDownload);
    }

    [Fact]
    public void Validate_InvalidRetryAndThemeSettings_ReturnsErrors()
    {
        using var directory = new TemporaryDirectory();
        var result = SampleConfigurationValidator.Validate(CreateValidInput(directory) with
        {
            MaximumRetryAttempts = -1,
            RetryDelayMilliseconds = -10,
            DialogTheme = (UpdateKit.WinForms.ApplicationTheme)99,
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("MaxRetryAttempts", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("InitialDelay", StringComparison.Ordinal));
        Assert.Contains("Choose a supported dialog theme.", result.Errors);
    }

    [Fact]
    public void CreateDialogOptions_ExtensionSelection_DelegatesToUpdateClient()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            AssetSelectionMode = SampleAssetSelectionMode.Extension,
            AssetSelectionValue = "ZIP",
        };
        var configuration = AssertValid(input);
        using var httpClient = CreateNetworkRejectingClient();
        var client = new UpdateClient(httpClient, configuration.CreateClientOptions());

        var options = configuration.CreateDialogOptions(client);
        var release = CreateRelease(
            new ReleaseAsset("update.exe", new Uri("https://example.invalid/update.exe"), 1),
            new ReleaseAsset("update.zip", new Uri("https://example.invalid/update.zip"), 2));
        var selection = options.AssetSelector(release);

        Assert.True(selection.IsSuccess);
        Assert.Equal("update.zip", selection.Value.Name);
        Assert.Equal("1.2.3", options.CurrentVersion);
        Assert.Equal(configuration.DestinationFilePath, options.DestinationFilePath);
    }

    [Fact]
    public void CreateDialogOptions_DirectChecksum_MapsExpectedChecksum()
    {
        using var directory = new TemporaryDirectory();
        var checksum = new string('A', 64);
        var configuration = AssertValid(CreateValidInput(directory) with
        {
            VerificationMode = SampleVerificationMode.DirectSha256,
            VerificationValue = checksum,
        });
        using var httpClient = CreateNetworkRejectingClient();
        var client = new UpdateClient(httpClient, configuration.CreateClientOptions());

        var options = configuration.CreateDialogOptions(client);

        Assert.Equal(checksum, options.ExpectedSha256);
        Assert.Null(options.ChecksumAssetSelector);
    }

    [Fact]
    public void CreateDialogOptions_ChecksumFile_MapsChecksumAssetSelector()
    {
        using var directory = new TemporaryDirectory();
        var configuration = AssertValid(CreateValidInput(directory) with
        {
            VerificationMode = SampleVerificationMode.ChecksumFile,
            VerificationValue = "SHA256SUMS.txt",
        });
        using var httpClient = CreateNetworkRejectingClient();
        var client = new UpdateClient(httpClient, configuration.CreateClientOptions());

        var options = configuration.CreateDialogOptions(client);
        var checksumAsset = new ReleaseAsset(
            "SHA256SUMS.txt",
            new Uri("https://example.invalid/SHA256SUMS.txt"),
            100);
        var selection = options.ChecksumAssetSelector!(CreateRelease(checksumAsset));

        Assert.Null(options.ExpectedSha256);
        Assert.True(selection.IsSuccess);
        Assert.Same(checksumAsset, selection.Value);
    }

    [Fact]
    public void Validate_InvalidInputs_ReturnsActionableErrors()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            RepositoryOwner = " ",
            RepositoryName = "owner/repository",
            AccessToken = "invalid token",
            CurrentVersion = "1.2",
            AssetSelectionValue = " ",
            DestinationFilePath = @"relative\update.zip",
            VerificationMode = SampleVerificationMode.DirectSha256,
            VerificationValue = "not-a-checksum",
        };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("RepositoryOwner", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("RepositoryName", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("AccessToken", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.StartsWith("Current version:", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.StartsWith("Asset selection:", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("fully qualified", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("64 hexadecimal", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => result.Configuration);
    }

    [Fact]
    public void Validate_MissingDestinationDirectory_ReturnsError()
    {
        using var directory = new TemporaryDirectory();
        var missingDirectory = Path.Combine(directory.Path, "missing");
        var input = CreateValidInput(directory) with
        {
            DestinationFilePath = Path.Combine(missingDirectory, "update.zip"),
        };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("The destination directory does not exist.", result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_MissingChecksumFileName_ReturnsError(string? value)
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            VerificationMode = SampleVerificationMode.ChecksumFile,
            VerificationValue = value,
        };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.StartsWith("Checksum-file asset:", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UnsupportedModes_ReturnsErrors()
    {
        using var directory = new TemporaryDirectory();
        var input = CreateValidInput(directory) with
        {
            AssetSelectionMode = (SampleAssetSelectionMode)99,
            VerificationMode = (SampleVerificationMode)99,
        };

        var result = SampleConfigurationValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Choose a supported asset-selection mode.", result.Errors);
        Assert.Contains("Choose a supported checksum-verification mode.", result.Errors);
    }

    [Fact]
    public void Failure_CapturesErrorSnapshot()
    {
        var errors = new List<string> { "First error" };

        var result = SampleConfigurationResult.Failure(errors);
        errors.Add("Later error");

        Assert.False(result.IsValid);
        Assert.Equal(new[] { "First error" }, result.Errors);
    }

    private static SampleUpdateConfiguration AssertValid(SampleConfigurationInput input)
    {
        var result = SampleConfigurationValidator.Validate(input);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return result.Configuration;
    }

    private static SampleConfigurationInput CreateValidInput(TemporaryDirectory directory) =>
        new(
            "octocat",
            "hello-world",
            null,
            "1.2.3",
            false,
            SampleAssetSelectionMode.ExactName,
            "update.zip",
            Path.Combine(directory.Path, "update.zip"),
            SampleVerificationMode.None,
            null);

    private static HttpClient CreateNetworkRejectingClient() =>
        new(new NetworkRejectingHandler());

    private static ReleaseInfo CreateRelease(params ReleaseAsset[] assets) =>
        new(
            1,
            "2.0.0",
            "Version 2",
            "Release notes",
            new Uri("https://example.invalid/releases/2"),
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            false,
            false,
            assets);

    private sealed class NetworkRejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Configuration tests must not use the network.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "UpdateKit.Example.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
