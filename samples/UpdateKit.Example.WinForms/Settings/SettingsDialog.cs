using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms.Settings;

internal sealed class SettingsDialog : Form
{
    private readonly IApplicationSettingsStore _store;
    private readonly ApplicationSettings _defaults;
    private readonly Action<ApplicationTheme> _previewTheme;
    private readonly ComboBox _themeComboBox = new();
    private readonly CheckBox _includePrereleasesCheckBox = new();
    private readonly CheckBox _automaticCheckCheckBox = new();
    private readonly CheckBox _confirmDownloadCheckBox = new();
    private readonly CheckBox _openFolderCheckBox = new();
    private readonly CheckBox _rememberRepositoryCheckBox = new();
    private readonly CheckBox _rememberAssetCheckBox = new();
    private readonly CheckBox _rememberDestinationCheckBox = new();
    private readonly TextBox _downloadDirectoryTextBox = new();
    private readonly NumericUpDown _retryCountNumeric = new();
    private readonly NumericUpDown _retryDelayNumeric = new();
    private readonly Label _errorLabel = new();
    private readonly Button _saveButton = new();

    public SettingsDialog(
        IApplicationSettingsStore store,
        ApplicationSettings settings,
        ApplicationSettings defaults,
        Action<ApplicationTheme> previewTheme)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        SelectedSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _previewTheme = previewTheme ?? throw new ArgumentNullException(nameof(previewTheme));
        Font = SystemFonts.MessageBoxFont;
        InitializeDialog();
        Populate(settings);
        ApplyTheme();
    }

    public ApplicationSettings SelectedSettings { get; private set; }

    private void InitializeDialog()
    {
        Text = "UpdateKit example settings";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(650, 690);
        MinimumSize = new Size(590, 610);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var content = new FlowLayoutPanel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16),
            WrapContents = false,
        };
        content.Controls.Add(CreateAppearanceGroup());
        content.Controls.Add(CreateUpdateGroup());
        content.Controls.Add(CreateDownloadGroup());
        content.Controls.Add(CreatePrivacyGroup());
        content.Controls.Add(CreateErrorLabel());

        var clearButton = CreateButton("Clear saved settings", "Delete all saved example settings");
        clearButton.Click += ClearButton_Click;
        _saveButton.Text = "Save";
        _saveButton.AutoSize = true;
        _saveButton.MinimumSize = new Size(88, 30);
        _saveButton.Click += SaveButton_Click;
        var cancelButton = CreateButton("Cancel", "Close settings without saving");
        cancelButton.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 8, 16, 16),
            WrapContents = false,
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(clearButton);
        Controls.Add(content);
        Controls.Add(buttons);
        AcceptButton = _saveButton;
        CancelButton = cancelButton;
    }

    private GroupBox CreateAppearanceGroup()
    {
        var group = CreateGroup("Appearance");
        var layout = CreateGrid(1);
        ConfigureDropDown(_themeComboBox, "Application theme");
        _themeComboBox.Items.AddRange(["System", "Light", "Dark"]);
        _themeComboBox.SelectedIndexChanged += (_, _) =>
        {
            _previewTheme(GetTheme());
            ApplyTheme();
        };
        AddRow(layout, 0, "Theme:", _themeComboBox);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateUpdateGroup()
    {
        var group = CreateGroup("Update behavior");
        var panel = CreateCheckBoxPanel();
        AddCheckBox(panel, _includePrereleasesCheckBox, "Include prerelease versions by default");
        AddCheckBox(panel, _automaticCheckCheckBox, "Automatically check when the application starts");
        AddCheckBox(panel, _confirmDownloadCheckBox, "Ask for confirmation before downloading");
        AddCheckBox(panel, _openFolderCheckBox, "Open the destination folder after a successful download");
        AddCheckBox(panel, _rememberRepositoryCheckBox, "Remember repository owner and name");
        AddCheckBox(panel, _rememberAssetCheckBox, "Remember asset-selection mode and value");
        AddCheckBox(panel, _rememberDestinationCheckBox, "Remember the last destination directory");
        group.Controls.Add(panel);
        return group;
    }

    private GroupBox CreateDownloadGroup()
    {
        var group = CreateGroup("Download behavior");
        var layout = CreateGrid(3);
        _downloadDirectoryTextBox.Dock = DockStyle.Fill;
        _downloadDirectoryTextBox.AccessibleName = "Default download directory";
        var browseButton = CreateButton("Browse...", "Choose default download directory");
        browseButton.Click += (_, _) => BrowseDownloadDirectory();
        var directoryPanel = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        directoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        directoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        directoryPanel.Controls.Add(_downloadDirectoryTextBox, 0, 0);
        directoryPanel.Controls.Add(browseButton, 1, 0);

        _retryCountNumeric.Minimum = 0;
        _retryCountNumeric.Maximum = ApplicationSettings.MaximumAllowedRetries;
        _retryCountNumeric.AccessibleName = "Maximum retry count";
        _retryCountNumeric.Width = 100;
        _retryDelayNumeric.Minimum = 0;
        _retryDelayNumeric.Maximum = ApplicationSettings.MaximumRetryDelayMilliseconds / 1_000m;
        _retryDelayNumeric.DecimalPlaces = 1;
        _retryDelayNumeric.Increment = 0.5m;
        _retryDelayNumeric.AccessibleName = "Retry delay in seconds";
        _retryDelayNumeric.Width = 100;

        AddRow(layout, 0, "Default directory:", directoryPanel);
        AddRow(layout, 1, "Maximum retries:", _retryCountNumeric);
        AddRow(layout, 2, "Initial retry delay (seconds):", _retryDelayNumeric);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreatePrivacyGroup()
    {
        var group = CreateGroup("Privacy and security");
        group.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(570, 0),
            Text = "Repository details and preferences may be stored locally for this user. " +
                "GitHub access tokens are never logged, serialized, or persisted.",
            AccessibleName = "Settings privacy notice",
        });
        return group;
    }

    private Control CreateErrorLabel()
    {
        _errorLabel.AutoSize = true;
        _errorLabel.MaximumSize = new Size(590, 0);
        _errorLabel.Padding = new Padding(8);
        _errorLabel.Visible = false;
        _errorLabel.AccessibleName = "Settings validation error";
        return _errorLabel;
    }

    private void Populate(ApplicationSettings settings)
    {
        _themeComboBox.SelectedIndex = settings.Theme switch
        {
            ApplicationTheme.Light => 1,
            ApplicationTheme.Dark => 2,
            _ => 0,
        };
        _includePrereleasesCheckBox.Checked = settings.IncludePrereleaseVersions;
        _automaticCheckCheckBox.Checked = settings.AutomaticallyCheckForUpdates;
        _confirmDownloadCheckBox.Checked = settings.ConfirmBeforeDownload;
        _openFolderCheckBox.Checked = settings.OpenDestinationFolderAfterDownload;
        _rememberRepositoryCheckBox.Checked = settings.RememberRepository;
        _rememberAssetCheckBox.Checked = settings.RememberAssetSelection;
        _rememberDestinationCheckBox.Checked = settings.RememberDestinationDirectory;
        _downloadDirectoryTextBox.Text = settings.DefaultDownloadDirectory;
        _retryCountNumeric.Value = Math.Clamp(settings.MaximumRetryCount, 0, ApplicationSettings.MaximumAllowedRetries);
        _retryDelayNumeric.Value = Math.Clamp(
            settings.RetryDelayMilliseconds / 1_000m,
            _retryDelayNumeric.Minimum,
            _retryDelayNumeric.Maximum);
    }

    private ApplicationSettings ReadSettings() => SelectedSettings with
    {
        Theme = GetTheme(),
        IncludePrereleaseVersions = _includePrereleasesCheckBox.Checked,
        AutomaticallyCheckForUpdates = _automaticCheckCheckBox.Checked,
        ConfirmBeforeDownload = _confirmDownloadCheckBox.Checked,
        OpenDestinationFolderAfterDownload = _openFolderCheckBox.Checked,
        RememberRepository = _rememberRepositoryCheckBox.Checked,
        RememberAssetSelection = _rememberAssetCheckBox.Checked,
        RememberDestinationDirectory = _rememberDestinationCheckBox.Checked,
        DefaultDownloadDirectory = _downloadDirectoryTextBox.Text.Trim(),
        MaximumRetryCount = decimal.ToInt32(_retryCountNumeric.Value),
        RetryDelayMilliseconds = decimal.ToInt32(_retryDelayNumeric.Value * 1_000m),
    };

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        var settings = ReadSettings();
        var errors = ApplicationSettingsValidator.Validate(settings);
        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, errors));
            return;
        }

        try
        {
            _saveButton.Enabled = false;
            await _store.SaveAsync(settings);
            SelectedSettings = settings.Normalize(_defaults);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError($"Settings could not be saved: {exception.Message}");
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }

    private async void ClearButton_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show(
            this,
            "Clear all locally saved UpdateKit example settings?",
            "Clear saved settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _store.ClearAsync();
            SelectedSettings = _defaults;
            Populate(_defaults);
            _previewTheme(_defaults.Theme);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError($"Saved settings could not be cleared: {exception.Message}");
        }
    }

    private void BrowseDownloadDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the default folder for downloaded updates",
            SelectedPath = Directory.Exists(_downloadDirectoryTextBox.Text)
                ? _downloadDirectoryTextBox.Text
                : _defaults.DefaultDownloadDirectory,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _downloadDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
        WinFormsThemeManager.ApplyErrorStyle(_errorLabel, GetTheme());
    }

    private void ApplyTheme()
    {
        WinFormsThemeManager.ApplyTheme(this, GetTheme());
        if (_errorLabel.Visible)
        {
            WinFormsThemeManager.ApplyErrorStyle(_errorLabel, GetTheme());
        }
    }

    private ApplicationTheme GetTheme() => _themeComboBox.SelectedIndex switch
    {
        1 => ApplicationTheme.Light,
        2 => ApplicationTheme.Dark,
        _ => ApplicationTheme.System,
    };

    private static GroupBox CreateGroup(string text) => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(590, 0),
        Padding = new Padding(12, 8, 12, 12),
        Text = text,
    };

    private static TableLayoutPanel CreateGrid(int rows)
    {
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = rows, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static FlowLayoutPanel CreateCheckBoxPanel() => new()
    {
        AutoSize = true,
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
    };

    private static void AddCheckBox(Control panel, CheckBox checkBox, string text)
    {
        checkBox.AutoSize = true;
        checkBox.Text = text;
        checkBox.AccessibleName = text;
        checkBox.Margin = new Padding(0, 3, 0, 3);
        panel.Controls.Add(checkBox);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string text, Control control)
    {
        var label = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = text, Margin = new Padding(0, 6, 12, 6) };
        control.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureDropDown(ComboBox comboBox, string accessibleName)
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.AccessibleName = accessibleName;
    }

    private static Button CreateButton(string text, string accessibleName) => new()
    {
        AutoSize = true,
        MinimumSize = new Size(88, 30),
        Text = text,
        AccessibleName = accessibleName,
    };
}
