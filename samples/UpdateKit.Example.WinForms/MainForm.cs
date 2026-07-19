using UpdateKit.Example.WinForms.Configuration;
using UpdateKit.WinForms;

namespace UpdateKit.Example.WinForms;

internal sealed class MainForm : Form
{
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

    public MainForm()
    {
        Font = SystemFonts.MessageBoxFont;
        InitializeForm();
        SetInitialValues();
        UpdateAssetSelectionPrompt();
        UpdateVerificationPrompt();
    }

    private void InitializeForm()
    {
        SuspendLayout();

        Text = "UpdateKit WinForms Example";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 680);
        MinimumSize = new Size(680, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var rootLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 6,
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var introduction = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 16),
            Text = "Configure a GitHub repository and open the reusable UpdateKit dialog. " +
                "The example does not store credentials or configuration.",
            AccessibleName = "Example instructions",
        };

        rootLayout.Controls.Add(introduction, 0, 0);
        rootLayout.Controls.Add(CreateInputLayout(), 0, 1);
        rootLayout.Controls.Add(_validationLabel, 0, 2);
        rootLayout.Controls.Add(_statusLabel, 0, 3);
        rootLayout.Controls.Add(CreateButtonLayout(), 0, 4);

        Controls.Add(rootLayout);
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
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ConfigureTextBox(
            _repositoryOwnerTextBox,
            "GitHub repository owner",
            "For example: octocat");
        ConfigureTextBox(
            _repositoryNameTextBox,
            "GitHub repository name",
            "For example: Hello-World");
        ConfigureTextBox(
            _accessTokenTextBox,
            "Optional GitHub access token",
            "Optional; cleared after each check");
        _accessTokenTextBox.UseSystemPasswordChar = true;
        _accessTokenTextBox.AutoCompleteMode = AutoCompleteMode.None;

        ConfigureTextBox(
            _currentVersionTextBox,
            "Current semantic version",
            "For example: 1.0.0");

        _includePrereleasesCheckBox.AutoSize = true;
        _includePrereleasesCheckBox.Text = "Include prerelease versions";
        _includePrereleasesCheckBox.AccessibleName = "Include prerelease versions";

        ConfigureDropDown(_assetSelectionModeComboBox, "Asset selection mode");
        _assetSelectionModeComboBox.Items.AddRange(
            ["Exact asset name", "File extension"]);
        _assetSelectionModeComboBox.SelectedIndexChanged += (_, _) =>
            UpdateAssetSelectionPrompt();
        ConfigureTextBox(
            _assetSelectionValueTextBox,
            "Asset selection value",
            string.Empty);

        ConfigureTextBox(
            _destinationTextBox,
            "Destination file path",
            "Choose where the downloaded asset will be saved");
        ConfigureButton(_browseButton, "Browse…", "Choose destination file");
        _browseButton.Click += BrowseButton_Click;

        ConfigureDropDown(_verificationModeComboBox, "Checksum verification mode");
        _verificationModeComboBox.Items.AddRange(
            ["No checksum verification", "Direct SHA-256", "Checksum-file asset"]);
        _verificationModeComboBox.SelectedIndexChanged += (_, _) =>
            UpdateVerificationPrompt();
        ConfigureTextBox(
            _verificationValueTextBox,
            "Checksum verification value",
            string.Empty);

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
        _validationLabel.BackColor = SystemColors.Info;
        _validationLabel.ForeColor = SystemColors.InfoText;
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
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
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
            Margin = new Padding(0),
            WrapContents = false,
        };
        layout.Controls.Add(_closeButton);
        layout.Controls.Add(_checkButton);
        return layout;
    }

    private static void ConfigureTextBox(
        TextBox textBox,
        string accessibleName,
        string placeholderText)
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

    private static void ConfigureButton(
        Button button,
        string text,
        string accessibleName)
    {
        button.AutoSize = true;
        button.Text = text;
        button.AccessibleName = accessibleName;
    }

    private static void AddInputRow(
        TableLayoutPanel layout,
        int row,
        string labelText,
        Control control)
    {
        var label = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 8),
            Text = labelText,
        };
        AddInputRow(layout, row, label, control);
    }

    private static void AddInputRow(
        TableLayoutPanel layout,
        int row,
        Label label,
        Control control)
    {
        label.AutoSize = true;
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(0, 6, 12, 8);
        control.Margin = new Padding(0, 2, 0, 6);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void SetInitialValues()
    {
        _currentVersionTextBox.Text = GetApplicationSemanticVersion();
        _assetSelectionModeComboBox.SelectedIndex = 1;
        _assetSelectionValueTextBox.Text = ".zip";
        _destinationTextBox.Text = GetDefaultDestinationPath();
        _verificationModeComboBox.SelectedIndex = 0;
    }

    private void UpdateAssetSelectionPrompt()
    {
        var exactName = _assetSelectionModeComboBox.SelectedIndex == 0;
        _assetSelectionValueLabel.Text = exactName ? "Asset name:" : "File extension:";
        _assetSelectionValueTextBox.AccessibleName = exactName
            ? "Exact release asset name"
            : "Release asset file extension";
        _assetSelectionValueTextBox.PlaceholderText = exactName
            ? "For example: UpdateKit-win-x64.zip"
            : "For example: .zip";
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
        _verificationValueTextBox.AccessibleName = mode switch
        {
            SampleVerificationMode.DirectSha256 => "Expected SHA-256 checksum",
            SampleVerificationMode.ChecksumFile => "Checksum-file release asset name",
            _ => "Checksum verification is disabled",
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
            InitialDirectory = GetExistingDirectory(_destinationTextBox.Text),
            OverwritePrompt = true,
            RestoreDirectory = true,
            Title = "Choose update destination",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _destinationTextBox.Text = dialog.FileName;
        }
    }

    private void CheckButton_Click(object? sender, EventArgs e)
    {
        var validation = SampleConfigurationValidator.Validate(ReadInput());
        if (!validation.IsValid)
        {
            ShowValidationErrors(validation.Errors);
            _repositoryOwnerTextBox.Focus();
            return;
        }

        _validationLabel.Visible = false;
        _checkButton.Enabled = false;
        _browseButton.Enabled = false;
        UseWaitCursor = true;

        try
        {
            var configuration = validation.Configuration;
            using var httpClient = new HttpClient();
            var client = new UpdateClient(httpClient, configuration.CreateClientOptions());
            using var updateDialog = new UpdateDialog(configuration.CreateDialogOptions(client));

            updateDialog.ShowDialog(this);
            ShowDialogOutcome(updateDialog);
        }
        catch (UpdateConfigurationException exception)
        {
            ShowValidationErrors(exception.ValidationErrors);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            _statusLabel.Text = $"The update dialog could not be opened: {exception.Message}";
            MessageBox.Show(
                this,
                exception.Message,
                "UpdateKit example",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _accessTokenTextBox.Clear();
            _checkButton.Enabled = true;
            _browseButton.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private SampleConfigurationInput ReadInput() =>
        new(
            _repositoryOwnerTextBox.Text,
            _repositoryNameTextBox.Text,
            _accessTokenTextBox.Text,
            _currentVersionTextBox.Text,
            _includePrereleasesCheckBox.Checked,
            GetAssetSelectionMode(),
            _assetSelectionValueTextBox.Text,
            _destinationTextBox.Text,
            GetVerificationMode(),
            _verificationValueTextBox.Text);

    private SampleAssetSelectionMode GetAssetSelectionMode() =>
        _assetSelectionModeComboBox.SelectedIndex == 0
            ? SampleAssetSelectionMode.ExactName
            : SampleAssetSelectionMode.Extension;

    private SampleVerificationMode GetVerificationMode() =>
        _verificationModeComboBox.SelectedIndex switch
        {
            1 => SampleVerificationMode.DirectSha256,
            2 => SampleVerificationMode.ChecksumFile,
            _ => SampleVerificationMode.None,
        };

    private void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        _validationLabel.Text = "Please correct the following:\r\n• " +
            string.Join("\r\n• ", errors);
        _validationLabel.Visible = true;
        _statusLabel.Text = "The update dialog was not opened because the configuration is invalid.";
    }

    private void ShowDialogOutcome(UpdateDialog dialog)
    {
        if (dialog.DownloadResult is { } download)
        {
            _statusLabel.Text = $"Update downloaded successfully to {download.FilePath}.";
        }
        else if (dialog.LastError is { } error)
        {
            _statusLabel.Text = $"Last update operation: {error.Message}";
        }
        else if (dialog.CheckResult is { IsUpdateAvailable: false } check)
        {
            _statusLabel.Text = $"No update is available. Latest release: {check.LatestRelease.TagName}.";
        }
        else if (dialog.CheckResult is { IsUpdateAvailable: true } available)
        {
            _statusLabel.Text =
                $"Release {available.LatestRelease.TagName} is available; no download was completed.";
        }
        else
        {
            _statusLabel.Text = "The update dialog was closed before a check completed.";
        }
    }

    private static string GetApplicationSemanticVersion()
    {
        var version = typeof(MainForm).Assembly.GetName().Version;
        return version is null
            ? "1.0.0"
            : $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }

    private static string GetDefaultDestinationPath()
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            directory = Path.GetTempPath();
        }

        return Path.Combine(directory, "UpdateKit-update.zip");
    }

    private static string GetExistingDirectory(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            return directory is not null && Directory.Exists(directory)
                ? directory
                : Path.GetTempPath();
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            return Path.GetTempPath();
        }
    }
}
