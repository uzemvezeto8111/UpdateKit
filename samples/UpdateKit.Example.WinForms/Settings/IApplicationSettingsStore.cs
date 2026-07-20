namespace UpdateKit.Example.WinForms.Settings;

internal interface IApplicationSettingsStore
{
    string SettingsFilePath { get; }
    Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

internal sealed record ApplicationSettingsLoadResult(
    ApplicationSettings Settings,
    string? Warning = null);
