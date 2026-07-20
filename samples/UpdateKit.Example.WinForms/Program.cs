namespace UpdateKit.Example.WinForms;

using UpdateKit.Example.WinForms.Services;
using UpdateKit.Example.WinForms.Settings;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var defaults = ApplicationSettingsDefaults.Create();
        var store = new JsonApplicationSettingsStore(
            JsonApplicationSettingsStore.GetDefaultSettingsFilePath(),
            defaults);
        var loaded = store.LoadAsync().GetAwaiter().GetResult();
        Application.Run(new MainForm(
            store,
            loaded.Settings,
            defaults,
            loaded.Warning,
            new DestinationFolderLauncher()));
    }
}
