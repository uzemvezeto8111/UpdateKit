using System.Windows;

namespace UpdateKit.Minimal.Wpf;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose,
        };
        application.Run(new MainWindow());
    }
}
