using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using UpdateKit.Wpf;

namespace UpdateKit.Minimal.Wpf;

internal sealed class MainWindow : Window
{
    // CHANGE THESE FIVE VALUES for your application.
    private const string RepositoryOwner = "uzemvezeto8111"; // CHANGE THIS.
    private const string RepositoryName = "UpdateKit"; // CHANGE THIS.
    private const string CurrentVersion = "0.0.0"; // CHANGE THIS to your installed version.
    private const string AssetExtension = ".nupkg"; // CHANGE THIS to your release asset type.
    private static readonly string DestinationPath = // CHANGE THIS to your installer/package path.
        Path.Combine(Path.GetTempPath(), "UpdateKit.Minimal.Wpf-update.nupkg");

    private readonly HttpClient _httpClient;
    private readonly UpdateClient _updateClient;

    public MainWindow()
    {
        _httpClient = new HttpClient();
        _updateClient = new UpdateClient(
            _httpClient,
            new UpdateClientOptions
            {
                RepositoryOwner = RepositoryOwner,
                RepositoryName = RepositoryName,
                IncludePrereleases = true,
                UserAgent = "UpdateKit.Minimal.Wpf",
            });

        Title = "Minimal UpdateKit WPF Sample";
        Width = 380;
        Height = 170;
        MinWidth = 320;
        MinHeight = 140;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var checkButton = new Button
        {
            Content = "Check for updates",
            MinWidth = 140,
            Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsDefault = true,
        };
        AutomationProperties.SetName(checkButton, "Check for updates");
        checkButton.Click += CheckButton_Click;
        Content = checkButton;
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient.Dispose();
        base.OnClosed(e);
    }

    private void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new UpdateWindowOptions(
            _updateClient,
            CurrentVersion,
            DestinationPath,
            release => _updateClient.SelectAssetByExtension(release, AssetExtension))
        {
            WindowTitle = "Software Update",
        };

        var updateWindow = new UpdateWindow(options)
        {
            Owner = this,
        };
        updateWindow.ShowDialog();
    }
}
