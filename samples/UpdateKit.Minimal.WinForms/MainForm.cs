using UpdateKit.WinForms;

namespace UpdateKit.Minimal.WinForms;

internal sealed class MainForm : Form
{
    // CHANGE THESE FIVE VALUES for your application.
    private const string RepositoryOwner = "uzemvezeto8111"; // CHANGE THIS.
    private const string RepositoryName = "UpdateKit"; // CHANGE THIS.
    private const string CurrentVersion = "0.0.0"; // CHANGE THIS to your installed version.
    private const string AssetExtension = ".nupkg"; // CHANGE THIS to your release asset type.
    private static readonly string DestinationPath = // CHANGE THIS to your installer/package path.
        Path.Combine(Path.GetTempPath(), "UpdateKit.Minimal.WinForms-update.nupkg");

    private readonly HttpClient _httpClient;
    private readonly UpdateClient _updateClient;
    private readonly Button _checkForUpdatesButton = new();

    public MainForm()
    {
        _httpClient = new HttpClient();
        _updateClient = new UpdateClient(
            _httpClient,
            new UpdateClientOptions
            {
                RepositoryOwner = RepositoryOwner,
                RepositoryName = RepositoryName,
                IncludePrereleases = true,
                UserAgent = "UpdateKit.Minimal.WinForms",
            });

        Text = "Minimal UpdateKit Sample";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 120);

        _checkForUpdatesButton.Text = "Check for updates";
        _checkForUpdatesButton.AutoSize = true;
        _checkForUpdatesButton.Anchor = AnchorStyles.None;
        _checkForUpdatesButton.AccessibleName = "Check for updates";
        _checkForUpdatesButton.Click += CheckForUpdatesButton_Click;

        Controls.Add(_checkForUpdatesButton);
        AcceptButton = _checkForUpdatesButton;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _checkForUpdatesButton.Location = new Point(
            (ClientSize.Width - _checkForUpdatesButton.Width) / 2,
            (ClientSize.Height - _checkForUpdatesButton.Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CheckForUpdatesButton_Click(object? sender, EventArgs e)
    {
        var options = new UpdateDialogOptions(
            _updateClient,
            CurrentVersion,
            DestinationPath,
            release => _updateClient.SelectAssetByExtension(release, AssetExtension))
        {
            DialogTitle = "Software Update",
        };

        using var dialog = new UpdateDialog(options);
        dialog.ShowDialog(this);
    }
}
