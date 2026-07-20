namespace UpdateKit.Example.WinForms.Settings;

internal static class ApplicationSettingsDefaults
{
    public static ApplicationSettings Create()
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            directory = Path.GetTempPath();
        }

        return ApplicationSettings.CreateDefaults(directory);
    }
}
