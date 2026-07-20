using System.Globalization;
using UpdateKit.WinForms.Internal;

namespace UpdateKit.WinForms;

/// <summary>
/// A reusable, single-use update dialog. Create a new instance each time the dialog is shown.
/// The dialog does not own or dispose the configured <see cref="UpdateClient"/>.
/// </summary>
public sealed class UpdateDialog : Form
{
    private readonly UpdateDialogOptions _options;
    private readonly UpdateDialogController _controller;
    private readonly Font _headingFont;
    private readonly Label _headingLabel = new();
    private readonly Label _releaseNameLabel = new();
    private readonly Label _versionValueLabel = new();
    private readonly Label _publishedValueLabel = new();
    private readonly Label _assetValueLabel = new();
    private readonly TextBox _releaseNotesTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _bytesLabel = new();
    private readonly Label _errorLabel = new();
    private readonly Button _primaryButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _closeButton = new();

    private bool _shown;
    private bool _closeWhenIdle;

    /// <summary>Creates a single-use update dialog.</summary>
    public UpdateDialog(UpdateDialogOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        _controller = new UpdateDialogController(options);
        _controller.StateChanged += Controller_StateChanged;

        Font = SystemFonts.MessageBoxFont;
        _headingFont = new Font(Font, FontStyle.Bold);
        InitializeDialog();
        ApplyConfiguredTheme();
        ApplyState(_controller.State);
    }

    /// <summary>Gets the latest completed check result, when available.</summary>
    public UpdateCheckResult? CheckResult => _controller.State.CheckResult;

    /// <summary>Gets the selected primary release asset, when available.</summary>
    public ReleaseAsset? SelectedAsset => _controller.State.SelectedAsset;

    /// <summary>Gets the successfully completed download result, when available.</summary>
    public DownloadResult? DownloadResult => _controller.State.DownloadResult;

    /// <summary>Gets the most recent operational error, when available.</summary>
    public UpdateError? LastError => _controller.State.Error;

    /// <summary>Gets whether a check, download, verification, or cancellation is active.</summary>
    public bool IsOperationInProgress => _controller.State.IsBusy;

    /// <summary>Starts an update check when the current dialog state permits it.</summary>
    /// <returns><see langword="true"/> when an operation was started; otherwise <see langword="false"/>.</returns>
    public Task<bool> CheckForUpdateAsync() => _controller.CheckForUpdateAsync();

    /// <summary>Starts the configured download and verification workflow when the current state permits it.</summary>
    /// <returns><see langword="true"/> when an operation was started; otherwise <see langword="false"/>.</returns>
    public Task<bool> DownloadAsync() => _controller.DownloadAsync();

    /// <summary>Requests cancellation of the current operation.</summary>
    /// <returns><see langword="true"/> when cancellation was requested; otherwise <see langword="false"/>.</returns>
    public bool CancelOperation() => _controller.CancelOperation();

    /// <inheritdoc />
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyConfiguredTheme();
        ApplyState(_controller.State);

        if (_shown)
        {
            return;
        }

        _shown = true;
        if (_options.CheckForUpdateOnShown)
        {
            await _controller.CheckForUpdateAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_controller.RequestClose())
        {
            _closeWhenIdle = true;
            e.Cancel = true;
        }
        else if (DialogResult == DialogResult.None)
        {
            DialogResult = _controller.State.Status == UpdateDialogStatus.Succeeded
                ? DialogResult.OK
                : DialogResult.Cancel;
        }

        base.OnFormClosing(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.StateChanged -= Controller_StateChanged;
            _controller.Dispose();
            _headingFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeDialog()
    {
        SuspendLayout();

        Text = _options.DialogTitle;
        AccessibleName = _options.DialogTitle;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 560);
        MinimumSize = new Size(560, 480);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var rootLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 8,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ConfigureHeading();
        rootLayout.Controls.Add(_headingLabel, 0, 0);
        rootLayout.Controls.Add(_releaseNameLabel, 0, 1);
        rootLayout.Controls.Add(CreateDetailsLayout(), 0, 2);
        rootLayout.Controls.Add(CreateReleaseNotesLayout(), 0, 3);
        rootLayout.Controls.Add(_statusLabel, 0, 4);
        rootLayout.Controls.Add(CreateProgressLayout(), 0, 5);
        rootLayout.Controls.Add(_errorLabel, 0, 6);
        rootLayout.Controls.Add(CreateButtonLayout(), 0, 7);

        Controls.Add(rootLayout);
        ResumeLayout(performLayout: true);
    }

    private void ConfigureHeading()
    {
        _headingLabel.AutoSize = true;
        _headingLabel.Dock = DockStyle.Fill;
        _headingLabel.Font = _headingFont;
        _headingLabel.Margin = new Padding(0, 0, 0, 6);
        _headingLabel.AccessibleName = "Update status heading";

        _releaseNameLabel.AutoEllipsis = true;
        _releaseNameLabel.AutoSize = true;
        _releaseNameLabel.Dock = DockStyle.Fill;
        _releaseNameLabel.Margin = new Padding(0, 0, 0, 12);
        _releaseNameLabel.AccessibleName = "Release name";
    }

    private Control CreateDetailsLayout()
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddDetailRow(layout, 0, "Available version:", _versionValueLabel, "Available version");
        AddDetailRow(layout, 1, "Published:", _publishedValueLabel, "Publication date");
        AddDetailRow(layout, 2, "Asset:", _assetValueLabel, "Selected release asset");
        return layout;
    }

    private static void AddDetailRow(
        TableLayoutPanel layout,
        int row,
        string labelText,
        Label valueLabel,
        string accessibleName)
    {
        var nameLabel = new Label
        {
            AutoSize = true,
            Text = labelText,
            Margin = new Padding(0, 2, 12, 4),
            Anchor = AnchorStyles.Left,
        };
        valueLabel.AutoEllipsis = true;
        valueLabel.AutoSize = true;
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Margin = new Padding(0, 2, 0, 4);
        valueLabel.AccessibleName = accessibleName;

        layout.Controls.Add(nameLabel, 0, row);
        layout.Controls.Add(valueLabel, 1, row);
    }

    private Control CreateReleaseNotesLayout()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            AutoSize = true,
            Text = "Release notes:",
            Margin = new Padding(0, 0, 0, 4),
        };

        _releaseNotesTextBox.Dock = DockStyle.Fill;
        _releaseNotesTextBox.Multiline = true;
        _releaseNotesTextBox.ReadOnly = true;
        _releaseNotesTextBox.ScrollBars = ScrollBars.Vertical;
        _releaseNotesTextBox.BackColor = SystemColors.Window;
        _releaseNotesTextBox.AccessibleName = "Release notes";

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_releaseNotesTextBox, 0, 1);
        return layout;
    }

    private Control CreateProgressLayout()
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
        };

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Height = 20;
        _progressBar.AccessibleName = "Download progress";

        _bytesLabel.AutoSize = true;
        _bytesLabel.Dock = DockStyle.Fill;
        _bytesLabel.Margin = new Padding(0, 4, 0, 0);
        _bytesLabel.TextAlign = ContentAlignment.MiddleRight;
        _bytesLabel.AccessibleName = "Downloaded bytes";

        layout.Controls.Add(_progressBar, 0, 0);
        layout.Controls.Add(_bytesLabel, 0, 1);
        return layout;
    }

    private Control CreateButtonLayout()
    {
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 0, 0, 2);
        _statusLabel.AccessibleName = "Operation status";

        _errorLabel.AutoSize = true;
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.Margin = new Padding(0, 4, 0, 10);
        _errorLabel.AccessibleName = "Update error";

        _primaryButton.AutoSize = true;
        _primaryButton.MinimumSize = new Size(112, 30);
        _primaryButton.AccessibleName = "Primary update action";
        _primaryButton.Click += PrimaryButton_Click;

        _cancelButton.AutoSize = true;
        _cancelButton.MinimumSize = new Size(88, 30);
        _cancelButton.Text = "Cancel";
        _cancelButton.AccessibleName = "Cancel update operation";
        _cancelButton.Click += (_, _) => _controller.CancelOperation();

        _closeButton.AutoSize = true;
        _closeButton.MinimumSize = new Size(88, 30);
        _closeButton.Text = "Close";
        _closeButton.AccessibleName = "Close update dialog";
        _closeButton.Click += (_, _) => Close();

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false,
        };
        layout.Controls.Add(_closeButton);
        layout.Controls.Add(_cancelButton);
        layout.Controls.Add(_primaryButton);
        return layout;
    }

    private async void PrimaryButton_Click(object? sender, EventArgs e)
    {
        var state = _controller.State;
        if (state.CanCheck)
        {
            await _controller.CheckForUpdateAsync();
        }
        else if (state.CanDownload)
        {
            if (_options.ConfirmBeforeDownload && !ConfirmDownload())
            {
                return;
            }

            await _controller.DownloadAsync();
        }
    }

    private bool ConfirmDownload()
    {
        using var dialog = new Form
        {
            Text = "Confirm download",
            AccessibleName = "Confirm update download",
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = Font,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
        };

        var message = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Text = $"Download {_controller.State.SelectedAsset?.Name ?? "the selected update"} " +
                $"to {_options.DestinationFilePath}?",
            AccessibleName = "Download confirmation message",
        };
        var downloadButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(96, 30),
            Text = "Download",
            DialogResult = DialogResult.OK,
        };
        var cancelButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(88, 30),
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(downloadButton);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
        };
        layout.Controls.Add(message, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = downloadButton;
        dialog.CancelButton = cancelButton;

        if (_options.Theme is { } theme)
        {
            WinFormsThemeManager.ApplyTheme(dialog, theme);
        }

        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private void ApplyConfiguredTheme()
    {
        if (_options.Theme is not { } theme)
        {
            return;
        }

        WinFormsThemeManager.ApplyTheme(this, theme);
        WinFormsThemeManager.ApplyErrorStyle(_errorLabel, theme);
    }

    private void Controller_StateChanged(UpdateDialogViewState state)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action<UpdateDialogViewState>(Controller_StateChanged), state);
            }
            catch (InvalidOperationException) when (IsDisposed || Disposing)
            {
            }

            return;
        }

        ApplyState(state);

        if (_closeWhenIdle && state.CanClose)
        {
            _closeWhenIdle = false;
            BeginInvoke(Close);
        }
    }

    private void ApplyState(UpdateDialogViewState state)
    {
        var release = state.CheckResult?.LatestRelease;
        _headingLabel.Text = HeadingFor(state.Status);
        _releaseNameLabel.Text = release?.Name ?? "No release selected";
        _versionValueLabel.Text = release?.TagName ?? "—";
        _publishedValueLabel.Text = release?.PublishedAt?.ToLocalTime()
            .ToString("g", CultureInfo.CurrentCulture) ?? "—";
        _assetValueLabel.Text = state.SelectedAsset is null
            ? "—"
            : $"{state.SelectedAsset.Name} ({FormatBytes(state.SelectedAsset.Size)})";
        _releaseNotesTextBox.Text = string.IsNullOrWhiteSpace(release?.Body)
            ? "No release notes were provided."
            : release.Body;
        _statusLabel.Text = StatusTextFor(state);

        _errorLabel.Text = state.Error is null ? string.Empty : $"Error: {state.Error.Message}";
        _errorLabel.Visible = state.Error is not null;

        ApplyProgress(state);
        ApplyButtons(state);
        UseWaitCursor = state.IsBusy;
        ApplyConfiguredTheme();
    }

    private void ApplyProgress(UpdateDialogViewState state)
    {
        var useMarquee = state.Status == UpdateDialogStatus.Checking ||
            state.Status is UpdateDialogStatus.Downloading or UpdateDialogStatus.Canceling &&
            state.Progress?.TotalBytes is null;

        _progressBar.Style = useMarquee
            ? ProgressBarStyle.Marquee
            : ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = useMarquee ? 30 : 0;

        if (!useMarquee)
        {
            var percentage = state.Status == UpdateDialogStatus.Succeeded
                ? 100d
                : state.Progress?.Percentage ?? 0d;
            _progressBar.Value = Math.Clamp((int)Math.Round(percentage), 0, 100);
        }

        _bytesLabel.Text = state.Progress switch
        {
            { TotalBytes: { } total } progress =>
                $"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(total)}",
            { } progress => $"{FormatBytes(progress.BytesDownloaded)} downloaded",
            _ => string.Empty,
        };
    }

    private void ApplyButtons(UpdateDialogViewState state)
    {
        if (state.CanCheck)
        {
            _primaryButton.Text = state.Status == UpdateDialogStatus.Initial
                ? "Check for updates"
                : "Try again";
        }
        else
        {
            _primaryButton.Text = state.Status is UpdateDialogStatus.Failed or UpdateDialogStatus.Canceled
                ? "Try again"
                : "Download";
        }

        _primaryButton.Enabled = state.CanCheck || state.CanDownload;
        _cancelButton.Enabled = state.CanCancel;
        _closeButton.Enabled = state.CanClose;

        AcceptButton = _primaryButton.Enabled ? _primaryButton : _closeButton;
        CancelButton = state.CanCancel ? _cancelButton : _closeButton;
    }

    private static string HeadingFor(UpdateDialogStatus status) => status switch
    {
        UpdateDialogStatus.Initial => "Check for updates",
        UpdateDialogStatus.Checking => "Checking for updates…",
        UpdateDialogStatus.NoUpdate => "You're up to date",
        UpdateDialogStatus.UpdateAvailable => "An update is available",
        UpdateDialogStatus.Downloading => "Downloading update…",
        UpdateDialogStatus.Canceling => "Canceling…",
        UpdateDialogStatus.Succeeded => "Update downloaded",
        UpdateDialogStatus.Canceled => "Operation canceled",
        UpdateDialogStatus.Failed => "Update failed",
        _ => "Software update",
    };

    private static string StatusTextFor(UpdateDialogViewState state) => state.Status switch
    {
        UpdateDialogStatus.Initial => "Ready to check for an update.",
        UpdateDialogStatus.Checking => "Contacting the release service…",
        UpdateDialogStatus.NoUpdate =>
            $"Version {state.CheckResult?.CurrentVersion} is already current.",
        UpdateDialogStatus.UpdateAvailable => "Review the release and choose Download to continue.",
        UpdateDialogStatus.Downloading => "Downloading the selected release asset…",
        UpdateDialogStatus.Canceling => "Waiting for the active operation to stop…",
        UpdateDialogStatus.Succeeded =>
            $"The update was saved to {state.DownloadResult?.FilePath}.",
        UpdateDialogStatus.Canceled => "The operation was canceled safely.",
        UpdateDialogStatus.Failed => "The operation could not be completed.",
        _ => string.Empty,
    };

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1_024 && unitIndex < units.Length - 1)
        {
            value /= 1_024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes.ToString("N0", CultureInfo.CurrentCulture)} {units[unitIndex]}"
            : $"{value.ToString("N1", CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }
}
