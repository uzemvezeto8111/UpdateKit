using UpdateKit.Example.WinForms.Settings;
using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms.Tests;

public sealed class JsonApplicationSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFileReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);

        var result = await store.LoadAsync();

        Assert.Equal(defaults, result.Settings);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsEverySupportedThemeAndPreferences()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);

        foreach (var theme in new[] { ApplicationTheme.System, ApplicationTheme.Light, ApplicationTheme.Dark })
        {
            var expected = defaults with
            {
                Theme = theme,
                IncludePrereleaseVersions = true,
                AutomaticallyCheckForUpdates = true,
                OpenDestinationFolderAfterDownload = true,
                RepositoryOwner = "owner",
                RepositoryName = "repository",
                MaximumRetryCount = 3,
                RetryDelayMilliseconds = 2_500,
            };

            await store.SaveAsync(expected);
            var actual = await store.LoadAsync();

            Assert.Equal(expected, actual.Settings);
            Assert.Null(actual.Warning);
            var json = await File.ReadAllTextAsync(store.SettingsFilePath);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LoadAsync_MalformedJsonReturnsDefaultsWithWarning()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await File.WriteAllTextAsync(store.SettingsFilePath, "{ definitely-not-json");

        var result = await store.LoadAsync();

        Assert.Equal(defaults, result.Settings);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task LoadAsync_PartialJsonUsesPropertyAndSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await File.WriteAllTextAsync(store.SettingsFilePath, """
            { "version": 1, "theme": "dark", "maximumRetryCount": 2 }
            """);

        var result = await store.LoadAsync();

        Assert.Equal(ApplicationTheme.Dark, result.Settings.Theme);
        Assert.Equal(2, result.Settings.MaximumRetryCount);
        Assert.Equal(defaults.DefaultDownloadDirectory, result.Settings.DefaultDownloadDirectory);
        Assert.Equal(".nupkg", result.Settings.AssetSelectionValue);
    }

    [Fact]
    public async Task LoadAsync_OutOfRangePartialValuesAreNormalizedToSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await File.WriteAllTextAsync(store.SettingsFilePath, """
            {
              "version": 1,
              "maximumRetryCount": -5,
              "retryDelayMilliseconds": 999999,
              "defaultDownloadDirectory": "relative",
              "lastDestinationDirectory": "missing"
            }
            """);

        var result = await store.LoadAsync();

        Assert.Equal(defaults.MaximumRetryCount, result.Settings.MaximumRetryCount);
        Assert.Equal(defaults.RetryDelayMilliseconds, result.Settings.RetryDelayMilliseconds);
        Assert.Equal(defaults.DefaultDownloadDirectory, result.Settings.DefaultDownloadDirectory);
        Assert.Null(result.Settings.LastDestinationDirectory);
    }

    [Fact]
    public async Task LoadAsync_FutureVersionReturnsDefaultsWithWarning()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await File.WriteAllTextAsync(store.SettingsFilePath, "{ \"version\": 999, \"theme\": \"dark\" }");

        var result = await store.LoadAsync();

        Assert.Equal(defaults, result.Settings);
        Assert.Contains("newer", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_AtomicallyReplacesExistingSettingsAndCleansTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await store.SaveAsync(defaults with { RepositoryOwner = "first" });

        await store.SaveAsync(defaults with { RepositoryOwner = "second" });

        var result = await store.LoadAsync();
        Assert.Equal("second", result.Settings.RepositoryOwner);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_CommitFailurePreservesExistingFileAndCleansTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var normalStore = CreateStore(directory, defaults);
        await normalStore.SaveAsync(defaults with { RepositoryOwner = "preserved" });
        var original = await File.ReadAllTextAsync(normalStore.SettingsFilePath);
        var failingStore = new JsonApplicationSettingsStore(
            normalStore.SettingsFilePath,
            defaults,
            new FailingCommitter());

        await Assert.ThrowsAsync<IOException>(() =>
            failingStore.SaveAsync(defaults with { RepositoryOwner = "not-written" }));

        Assert.Equal(original, await File.ReadAllTextAsync(normalStore.SettingsFilePath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task ClearAsync_RemovesSavedSettingsAndSubsequentLoadUsesDefaults()
    {
        using var directory = new TemporaryDirectory();
        var defaults = ApplicationSettings.CreateDefaults(directory.Path);
        var store = CreateStore(directory, defaults);
        await store.SaveAsync(defaults with { RepositoryOwner = "owner" });

        await store.ClearAsync();
        var result = await store.LoadAsync();

        Assert.False(File.Exists(store.SettingsFilePath));
        Assert.Equal(defaults, result.Settings);
    }

    private static JsonApplicationSettingsStore CreateStore(
        TemporaryDirectory directory,
        ApplicationSettings defaults) =>
        new(Path.Combine(directory.Path, "settings.json"), defaults);

    private sealed class FailingCommitter : IAtomicSettingsFileCommitter
    {
        public void Commit(string temporaryPath, string destinationPath) =>
            throw new IOException("Simulated commit failure.");
    }
}
