using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpdateKit.Example.WinForms.Settings;

internal sealed class JsonApplicationSettingsStore : IApplicationSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ApplicationSettings _defaults;
    private readonly IAtomicSettingsFileCommitter _committer;

    public JsonApplicationSettingsStore(
        string settingsFilePath,
        ApplicationSettings defaults,
        IAtomicSettingsFileCommitter? committer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        SettingsFilePath = Path.GetFullPath(settingsFilePath);
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _committer = committer ?? AtomicSettingsFileCommitter.Instance;
    }

    public string SettingsFilePath { get; }

    public async Task<ApplicationSettingsLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new(_defaults);
        }

        try
        {
            await using var stream = new FileStream(
                SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4_096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (settings is null)
            {
                return new(_defaults, "The saved settings file was empty; safe defaults were used.");
            }

            if (settings.Version > ApplicationSettings.CurrentVersion)
            {
                return new(
                    _defaults,
                    $"Settings version {settings.Version} is newer than this application supports; safe defaults were used.");
            }

            return new(settings.Normalize(_defaults));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new(
                _defaults,
                $"Saved settings could not be read; safe defaults were used. {exception.Message}");
        }
    }

    public async Task SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize(_defaults);
        var errors = ApplicationSettingsValidator.Validate(normalized);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));
        }

        var directory = Path.GetDirectoryName(SettingsFilePath)
            ?? throw new InvalidOperationException("The settings path has no containing directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(SettingsFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4_096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _committer.Commit(temporaryPath, SettingsFilePath);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(SettingsFilePath);
        return Task.CompletedTask;
    }

    public static string GetDefaultSettingsFilePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "UpdateKit", "Example.WinForms", "settings.json");
    }
}

internal interface IAtomicSettingsFileCommitter
{
    void Commit(string temporaryPath, string destinationPath);
}

internal sealed class AtomicSettingsFileCommitter : IAtomicSettingsFileCommitter
{
    public static AtomicSettingsFileCommitter Instance { get; } = new();

    private AtomicSettingsFileCommitter()
    {
    }

    public void Commit(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, destinationPath);
        }
    }
}
