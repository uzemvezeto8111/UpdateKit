using System.ComponentModel;
using System.Windows;

namespace UpdateKit.Wpf;

/// <summary>
/// A reusable, single-use WPF update window. Create a new instance each time it is shown.
/// The window does not own or dispose the configured <see cref="UpdateClient"/>.
/// </summary>
public sealed partial class UpdateWindow : Window
{
    private readonly UpdateWindowOptions _options;
    private bool _loaded;
    private bool _closeWhenIdle;
    private bool _disposed;

    /// <summary>Creates a single-use update window and its default bindable view model.</summary>
    public UpdateWindow(UpdateWindowOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        ViewModel = new UpdateWindowViewModel(options);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();
        Title = options.WindowTitle;
        DataContext = ViewModel;
    }

    /// <summary>Gets the bindable workflow state used by the window.</summary>
    public UpdateWindowViewModel ViewModel { get; }

    /// <summary>Gets the latest completed check result, when available.</summary>
    public UpdateCheckResult? CheckResult => ViewModel.CheckResult;

    /// <summary>Gets the selected primary release asset, when available.</summary>
    public ReleaseAsset? SelectedAsset => ViewModel.SelectedAsset;

    /// <summary>Gets the successfully completed download result, when available.</summary>
    public DownloadResult? DownloadResult => ViewModel.DownloadResult;

    /// <summary>Gets the most recent operational error, when available.</summary>
    public UpdateError? LastError => ViewModel.LastError;

    /// <summary>Gets whether a check, download, verification, or cancellation is active.</summary>
    public bool IsOperationInProgress => ViewModel.IsOperationInProgress;

    /// <summary>Starts an update check when the current window state permits it.</summary>
    public Task<bool> CheckForUpdateAsync() => ViewModel.CheckForUpdateAsync();

    /// <summary>Starts the configured download and verification workflow when permitted.</summary>
    public Task<bool> DownloadAsync() => ViewModel.DownloadAsync();

    /// <summary>Requests cancellation of the current operation.</summary>
    public bool CancelOperation() => ViewModel.CancelOperation();

    /// <inheritdoc />
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (_options.CheckForUpdateOnLoaded)
        {
            await ViewModel.CheckForUpdateAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ViewModel.RequestClose())
        {
            _closeWhenIdle = true;
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        DisposeViewModel();
        base.OnClosed(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_closeWhenIdle || !ViewModel.CanClose || _disposed)
        {
            return;
        }

        _closeWhenIdle = false;
        Dispatcher.BeginInvoke(Close);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void DisposeViewModel()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Dispose();
    }
}
