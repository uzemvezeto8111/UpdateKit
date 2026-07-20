using UpdateKit.Example.WinForms.Configuration;
using UpdateKit.Example.WinForms.Services;
using UpdateKit.Example.WinForms.Settings;
using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms;

internal sealed class MainForm : Form
{
    private const string DefaultDestinationFileName = "UpdateKit-update.zip";

    private readonly IApplicationSettingsStore _settingsStore;
    private readonly ApplicationSettings _defaults;
    private readonly DestinationFolderCompletionAction _folderCompletionAction;
    private readonly AutomaticUpdateCheckCoordinator _automaticCheckCoordinator = new();
    private ApplicationSettings _settings;
    private readonly string? _settingsWarning;
    private bool _operationActive;

    private readonly MenuStrip _menuStrip = new();
    private readonly TextBox _repositoryOwnerTextBox = new();
    private readonly TextBox _repositoryNameTextBox = new();
    private readonly TextBox _accessTokenTextBox = new();
    private readonly TextBox _currentVersionTextBox = new();
    private readonly CheckBox _includePrereleasesCheckBox = new();
    private readonly ComboBox _assetSelectionModeComboBox = new();
    private readonly Label _assetSelectionValueLabel = new();
    private readonly TextBox _assetSelectionValueTextBox = new();
    private readonly TextBox _destinationTextBox = new();
    private readonly Button _browseButton = new();
    private readonly ComboBox _verificationModeComboBox = new();
    private readonly Label _verificationValueLabel = new();
    private readonly TextBox _verificationValueTextBox = new();
    private readonly Label _validationLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _checkButton = new();
    private readonly Button _closeButton = new();

    public MainForm(
        IApplicationSettingsStore settingsStore,
        ApplicationSettings settings,
        ApplicationSettings defaults,
        string? settingsWarning,
        IDestinationFolderLauncher folderLauncher)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _settingsWarning = settingsWarning;
        _folderCompletionAction = new DestinationFolderCompletionAction(folderLauncher);

        Font = SystemFonts.MessageBoxFont;
        InitializeForm();
        ApplySettingsToForm();
        UpdateAssetSelectionPrompt();
        UpdateVerificationPrompt();
        ApplyTheme();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyTheme();
        if (!string.IsNullOrWhiteSpace(_settingsWarning))
        {
            _statusLabel.Text = _settingsWarning;
        }

        BeginInvoke(new Action(TryStartAutomaticCheck));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_operationActive)
        {
            PersistCurrentFormState();
        }

        base.OnFormClosing(e);
    }

    private void InitializeForm()
    {
        SuspendLayout();
        Text = "UpdateKit WinForms Example";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 710);
        MinimumSize = new Size(700, 620);
        StartPosition = FormStartPosition.CenterScreen;

        var toolsMenu = new ToolStripMenuItem("&Tools");
        var settingsItem = new ToolStripMenuItem("&Settings...")
        {
            ShortcutKeys = Keys.Control | Keys.Oemcomma,
            AccessibleName = "Open application settings",
        };
        settingsItem.Click += SettingsItem_Click;
        toolsMenu.DropDownItems.Add(settingsItem);
        _menuStrip.Items.Add(toolsMenu);
        _menuStrip.Dock = DockStyle.Top;

        var rootLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rootLayout.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 16),
            Text = "Configure a GitHub repository and open the reusable UpdateKit dialog. " +
                "Preferences may be stored locally; access tokens are never saved.",
            AccessibleName = "Example instructions",
        }, 0, 0);
        rootLayout.Controls.Add(CreateInputLayout(), 0, 1);
        rootLayout.Controls.Add(_validationLabel, 0, 2);
        rootLayout.Controls.Add(_statusLabel, 0, 3);
        rootLayout.Controls.Add(CreateButtonLayout(), 0, 4);

        Controls.Add(rootLayout);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        AcceptButton = _checkButton;
        CancelButton = _closeButton;
        ResumeLayout(performLayout: true);
    }

    private Control CreateInputLayout()
    {
        var layout = new TableLayoutPanel
        {
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 10,
            Dock = DockStyle.Fill,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ConfigureTextBox(_repositoryOwnerTextBox, "GitHub repository owner", "For example: octocat");
        ConfigureTextBox(_repositoryNameTextBox, "GitHub repository name", "For example: Hello-World");
        ConfigureTextBox(_accessTokenTextBox, "Optional GitHub access token", "Optional; never saved and cleared after each check");
        _accessTokenTextBox.UseSystemPasswordChar = true;
        _accessTokenTextBox.AutoCompleteMode = AutoCompleteMode.None;
        ConfigureTextBox(_currentVersionTextBox, "Current semantic version", "For example: 1.0.0");

        _includePrereleasesCheckBox.AutoSize = true;
        _includePrereleasesCheckBox.Text = "Include prerelease versions";
        _includePrereleasesCheckBox.AccessibleName = _includePrereleasesCheckBox.Text;

        ConfigureDropDown(_assetSelectionModeComboBox, "Asset selection mode");
        _assetSelectionModeComboBox.Items.AddRange(["Exact asset name", "File extension"]);
        _assetSelectionModeComboBox.SelectedIndexChanged += (_, _) => UpdateAssetSelectionPrompt();
        ConfigureTextBox(_assetSelectionValueTextBox, "Asset selection value", string.Empty);
        ConfigureTextBox(_destinationTextBox, "Destination file path", "Choose where the asset will be saved");
        ConfigureButton(_browseButton, "Browse...", "Choose destination file");
        _browseButton.Click += BrowseButton_Click;

        ConfigureDropDown(_verificationModeComboBox, "Checksum verification mode");
        _verificationModeComboBox.Items.AddRange(["No checksum verification", "Direct SHA-256", "Checksum-file asset"]);
        _verificationModeComboBox.SelectedIndexChanged += (_, _) => UpdateVerificationPrompt();
        ConfigureTextBox(_verificationValueTextBox, "Checksum verification value", string.Empty);

        AddInputRow(layout, 0, "Repository owner:", _repositoryOwnerTextBox);
        AddInputRow(layout, 1, "Repository name:", _repositoryNameTextBox);
        AddInputRow(layout, 2, "Access token:", _accessTokenTextBox);
        AddInputRow(layout, 3, "Current version:", _currentVersionTextBox);
        AddInputRow(layout, 4, string.Empty, _includePrereleasesCheckBox);
        AddInputRow(layout, 5, "Asset selection:", _assetSelectionModeComboBox);
        AddInputRow(layout, 6, _assetSelectionValueLabel, _assetSelectionValueTextBox);
        AddInputRow(layout, 7, "Destination:", CreateDestinationLayout());
        AddInputRow(layout, 8, "Verification:", _verificationModeComboBox);
        AddInputRow(layout, 9, _verificationValueLabel, _verificationValueTextBox);

        _validationLabel.AutoSize = true;
        _validationLabel.Dock = DockStyle.Fill;
        _validationLabel.Padding = new Padding(8);
        _validationLabel.Margin = new Padding(0, 12, 0, 8);
        _validationLabel.Visible = false;
        _validationLabel.AccessibleName = "Configuration validation errors";
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 4, 0, 12);
        _statusLabel.Text = "Enter repository details to begin.";
        _statusLabel.AccessibleName = "Last update result";
        return layout;
    }

    private Control CreateDestinationLayout()
    {
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _destinationTextBox.Dock = DockStyle.Fill;
        _browseButton.Margin = new Padding(8, 0, 0, 0);
        layout.Controls.Add(_destinationTextBox, 0, 0);
        layout.Controls.Add(_browseButton, 1, 0);
        return layout;
    }

    private Control CreateButtonLayout()
    {
        ConfigureButton(_checkButton, "Check for updates", "Open the UpdateKit dialog");
        _checkButton.MinimumSize = new Size(132, 32);
        _checkButton.Click += CheckButton_Click;
        ConfigureButton(_closeButton, "Close", "Close the example application");
        _closeButton.MinimumSize = new Size(88, 32);
        _closeButton.DialogResult = DialogResult.Cancel;
        _closeButton.Click += (_, _) => Close();
        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        layout.Controls.Add(_closeButton);
        layout.Controls.Add(_checkButton);
        return layout;
    }

    private void ApplySettingsToForm()
    {
        var state = MainFormSettingsMapper.ToFormState(_settings, DefaultDestinationFileName);
        _repositoryOwnerTextBox.Text = state.RepositoryOwner;
        _repositoryNameTextBox.Text = state.RepositoryName;
        _currentVersionTextBox.Text = GetApplicationSemanticVersion();
        _includePrereleasesCheckBox.Checked = state.IncludePrereleases;
        _assetSelectionModeComboBox.SelectedIndex = state.AssetSelectionMode == SampleAssetSelectionMode.ExactName ? 0 : 1;
        _assetSelectionValueTextBox.Text = state.AssetSelectionValue;
        _destinationTextBox.Text = state.DestinationFilePath;
        _verificationModeComboBox.SelectedIndex = 0;
        _accessTokenTextBox.Clear();
    }

    private void SettingsItem_Click(object? sender, EventArgs e)
    {
        var originalTheme = _settings.Theme;
        using var dialog = new SettingsDialog(
            _settingsStore,
            CaptureCurrentSettings(),
            _defaults,
            theme =>
            {
                _settings = _settings with { Theme = theme };
                ApplyTheme();
            });

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings = dialog.SelectedSettings;
            ApplySettingsToForm();
        }
        else
        {
            _settings = _settings with { Theme = originalTheme };
        }

        ApplyTheme();
    }

    private void CheckButton_Click(object? sender, EventArgs e) => TryOpenUpdateDialog();

    private void TryStartAutomaticCheck()
    {
        var validation = SampleConfigurationValidator.Validate(ReadInput());
        if (_automaticCheckCoordinator.TryBegin(
            _settings.AutomaticallyCheckForUpdates,
            validation.IsValid))
        {
            TryOpenUpdateDialog(validation);
        }
    }

    private void TryOpenUpdateDialog(SampleConfigurationResult? existingValidation = null)
    {
        if (_operationActive)
        {
            return;
        }

        var validation = existingValidation ?? SampleConfigurationValidator.Validate(ReadInput());
        if (!validation.IsValid)
        {
            ShowValidationErrors(validation.Errors);
            _repositoryOwnerTextBox.Focus();
            return;
        }

        _operationActive = true;
        _validationLabel.Visible = false;
        SetOperationControls(enabled: false);
        PersistCurrentFormState();

        try
        {
            var configuration = validation.Configuration;
            using var httpClient = new HttpClient();
            var client = new UpdateClient(httpClient, configuration.CreateClientOptions());
            using var updateDialog = new UpdateDialog(configuration.CreateDialogOptions(client));
            updateDialog.ShowDialog(this);
            ShowDialogOutcome(updateDialog);
            var folderResult = _folderCompletionAction.Run(
                _settings.OpenDestinationFolderAfterDownload,
                updateDialog.DownloadResult?.FilePath);
            if (folderResult is { IsSuccess: false })
            {
                _statusLabel.Text += $" The destination folder could not be opened: {folderResult.ErrorMessage}";
                MessageBox.Show(this, folderResult.ErrorMessage, "Open destination folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (UpdateConfigurationException exception)
        {
            ShowValidationErrors(exception.ValidationErrors);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _statusLabel.Text = $"The update dialog could not be opened: {exception.Message}";
            MessageBox.Show(this, exception.Message, "UpdateKit example", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _accessTokenTextBox.Clear();
            _operationActive = false;
            SetOperationControls(enabled: true);
        }
    }

    private SampleConfigurationInput ReadInput() => new(
        _repositoryOwnerTextBox.Text,
        _repositoryNameTextBox.Text,
        _accessTokenTextBox.Text,
        _currentVersionTextBox.Text,
        _includePrereleasesCheckBox.Checked,
        GetAssetSelectionMode(),
        _assetSelectionValueTextBox.Text,
        _destinationTextBox.Text,
        GetVerificationMode(),
        _verificationValueTextBox.Text,
        _settings.MaximumRetryCount,
        _settings.RetryDelayMilliseconds,
        _settings.Theme,
        _settings.ConfirmBeforeDownload);

    private ApplicationSettings CaptureCurrentSettings()
    {
        var captured = MainFormSettingsMapper.Capture(
            _settings,
            _repositoryOwnerTextBox.Text,
            _repositoryNameTextBox.Text,
            GetAssetSelectionMode(),
            _assetSelectionValueTextBox.Text,
            _destinationTextBox.Text);
        return captured with { IncludePrereleaseVersions = _includePrereleasesCheckBox.Checked };
    }

    private void PersistCurrentFormState()
    {
        _settings = CaptureCurrentSettings();
        try
        {
            _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _statusLabel.Text = $"Preferences could not be saved: {exception.Message}";
        }
    }

    private void SetOperationControls(bool enabled)
    {
        _checkButton.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _menuStrip.Enabled = enabled;
        UseWaitCursor = !enabled;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        WinFormsThemeManager.ApplyTheme(this, _settings.Theme);
        WinFormsThemeManager.ApplyInformationStyle(_validationLabel, _settings.Theme);
    }

    private void UpdateAssetSelectionPrompt()
    {
        var exactName = GetAssetSelectionMode() == SampleAssetSelectionMode.ExactName;
        _assetSelectionValueLabel.Text = exactName ? "Asset name:" : "File extension:";
        _assetSelectionValueTextBox.AccessibleName = exactName ? "Exact release asset name" : "Release asset file extension";
        _assetSelectionValueTextBox.PlaceholderText = exactName ? "For example: UpdateKit-win-x64.zip" : "For example: .zip";
    }

    private void UpdateVerificationPrompt()
    {
        var mode = GetVerificationMode();
        _verificationValueTextBox.Enabled = mode != SampleVerificationMode.None;
        _verificationValueLabel.Text = mode switch
        {
            SampleVerificationMode.DirectSha256 => "Expected SHA-256:",
            SampleVerificationMode.ChecksumFile => "Checksum asset name:",
            _ => "Checksum value:",
        };
        _verificationValueTextBox.PlaceholderText = mode switch
        {
            SampleVerificationMode.DirectSha256 => "64 hexadecimal characters",
            SampleVerificationMode.ChecksumFile => "For example: checksums.sha256",
            _ => string.Empty,
        };
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            CheckPathExists = true,
            FileName = Path.GetFileName(_destinationTextBox.Text),
            Filter = "All files (*.*)|*.*",
            InitialDirectory = GetExistingDirectory(_destinationTextBox.Text, _settings.DefaultDownloadDirectory),
            OverwritePrompt = true,
            RestoreDirectory = true,
            Title = "Choose update destination",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _destinationTextBox.Text = dialog.FileName;
        }
    }

    private void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        _validationLabel.Text = "Please correct the following:\r\n- " + string.Join("\r\n- ", errors);
        _validationLabel.Visible = true;
        _statusLabel.Text = "The update dialog was not opened because the configuration is invalid.";
    }

    private void ShowDialogOutcome(UpdateDialog dialog)
    {
        _statusLabel.Text = dialog.DownloadResult is { } download
            ? $"Update downloaded successfully to {download.FilePath}."
            : dialog.LastError is { } error
                ? $"Last update operation: {error.Message}"
                : dialog.CheckResult is { IsUpdateAvailable: false } check
                    ? $"No update is available. Latest release: {check.LatestRelease.TagName}."
                    : dialog.CheckResult is { IsUpdateAvailable: true } available
                        ? $"Release {available.LatestRelease.TagName} is available; no download was completed."
                        : "The update dialog was closed before a check completed.";
    }

    private SampleAssetSelectionMode GetAssetSelectionMode() =>
        _assetSelectionModeComboBox.SelectedIndex == 0 ? SampleAssetSelectionMode.ExactName : SampleAssetSelectionMode.Extension;

    private SampleVerificationMode GetVerificationMode() => _verificationModeComboBox.SelectedIndex switch
    {
        1 => SampleVerificationMode.DirectSha256,
        2 => SampleVerificationMode.ChecksumFile,
        _ => SampleVerificationMode.None,
    };

    private static string GetApplicationSemanticVersion()
    {
        var version = typeof(MainForm).Assembly.GetName().Version;
        return version is null ? "0.2.0" : $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }

    private static string GetExistingDirectory(string filePath, string fallback)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            return directory is not null && Directory.Exists(directory) ? directory : fallback;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return fallback;
        }
    }

    private static void ConfigureTextBox(TextBox textBox, string accessibleName, string placeholderText)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.AccessibleName = accessibleName;
        textBox.PlaceholderText = placeholderText;
    }

    private static void ConfigureDropDown(ComboBox comboBox, string accessibleName)
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.AccessibleName = accessibleName;
    }

    private static void ConfigureButton(Button button, string text, string accessibleName)
    {
        button.AutoSize = true;
        button.Text = text;
        button.AccessibleName = accessibleName;
    }

    private static void AddInputRow(TableLayoutPanel layout, int row, string labelText, Control control) =>
        AddInputRow(layout, row, new Label { Text = labelText }, control);

    private static void AddInputRow(TableLayoutPanel layout, int row, Label label, Control control)
    {
        label.AutoSize = true;
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(0, 6, 12, 8);
        control.Margin = new Padding(0, 2, 0, 6);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
