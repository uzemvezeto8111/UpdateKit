using System.Text.Json;
using UpdateKit.Example.WinForms.Configuration;
using UpdateKit.Example.WinForms.Services;
using UpdateKit.Example.WinForms.Settings;
using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms.Tests;

public sealed class ApplicationSettingsTests
{
    [Fact]
    public void Defaults_AreSafeAndCredentialFree()
    {
        using var directory = new TemporaryDirectory();
        var settings = ApplicationSettings.CreateDefaults(directory.Path);

        Assert.Equal(ApplicationSettings.CurrentVersion, settings.Version);
        Assert.Equal(ApplicationTheme.System, settings.Theme);
        Assert.True(settings.ConfirmBeforeDownload);
        Assert.True(settings.RememberRepository);
        Assert.True(settings.RememberAssetSelection);
        Assert.True(settings.RememberDestinationDirectory);
        Assert.Equal(0, settings.MaximumRetryCount);
        Assert.Equal(1_000, settings.RetryDelayMilliseconds);
        Assert.DoesNotContain(
            typeof(ApplicationSettings).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsUnreasonableRetryAndDirectoryValues()
    {
        var settings = new ApplicationSettings
        {
            DefaultDownloadDirectory = "relative",
            MaximumRetryCount = 11,
            RetryDelayMilliseconds = -1,
        };

        var errors = ApplicationSettingsValidator.Validate(settings);

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, error => error.Contains("retry count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Retry delay", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("absolute directory", StringComparison.Ordinal));
    }

    [Fact]
    public void Mapper_AppliesRememberedValuesAndNeverContainsCredentials()
    {
        using var directory = new TemporaryDirectory();
        var settings = ApplicationSettings.CreateDefaults(directory.Path) with
        {
            IncludePrereleaseVersions = true,
            RepositoryOwner = "owner",
            RepositoryName = "repository",
            AssetSelectionMode = SampleAssetSelectionMode.ExactName,
            AssetSelectionValue = "asset.zip",
            LastDestinationDirectory = directory.Path,
        };

        var state = MainFormSettingsMapper.ToFormState(settings, "download.zip");

        Assert.Equal("owner", state.RepositoryOwner);
        Assert.Equal("repository", state.RepositoryName);
        Assert.True(state.IncludePrereleases);
        Assert.Equal(SampleAssetSelectionMode.ExactName, state.AssetSelectionMode);
        Assert.Equal("asset.zip", state.AssetSelectionValue);
        Assert.Equal(Path.Combine(directory.Path, "download.zip"), state.DestinationFilePath);
        Assert.DoesNotContain("token", JsonSerializer.Serialize(settings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutomaticCheck_RequiresEligibilityAndStartsOnlyOnce()
    {
        var coordinator = new AutomaticUpdateCheckCoordinator();

        Assert.False(coordinator.TryBegin(enabled: false, configurationIsValid: true));
        Assert.False(coordinator.TryBegin(enabled: true, configurationIsValid: false));
        Assert.True(coordinator.TryBegin(enabled: true, configurationIsValid: true));
        Assert.False(coordinator.TryBegin(enabled: true, configurationIsValid: true));
    }

    [Fact]
    public void FolderCompletionAction_UsesInjectedBoundaryOnlyAfterSuccess()
    {
        var launcher = new RecordingFolderLauncher();
        var action = new DestinationFolderCompletionAction(launcher);

        Assert.Null(action.Run(enabled: false, @"C:\downloads\update.zip"));
        Assert.Null(action.Run(enabled: true, successfullyDownloadedFilePath: null));
        var result = action.Run(enabled: true, @"C:\downloads\update.zip");

        Assert.True(result!.IsSuccess);
        Assert.Equal(1, launcher.CallCount);
        Assert.Equal(@"C:\downloads\update.zip", launcher.LastPath);
    }

    private sealed class RecordingFolderLauncher : IDestinationFolderLauncher
    {
        public int CallCount { get; private set; }
        public string? LastPath { get; private set; }

        public FolderLaunchResult OpenContainingFolder(string filePath)
        {
            CallCount++;
            LastPath = filePath;
            return new(true);
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UpdateKit.Settings.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
