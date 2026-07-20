using System.Drawing;
using System.Windows.Forms;

namespace UpdateKit.WinForms.Tests;

public sealed class WinFormsThemeManagerTests
{
    [Theory]
    [InlineData(ApplicationTheme.Light)]
    [InlineData(ApplicationTheme.Dark)]
    public void ResolveTheme_ExplicitSelectionDoesNotConsultSystem(ApplicationTheme expected)
    {
        var provider = new StubSystemThemeProvider(ApplicationTheme.Dark);

        var actual = WinFormsThemeManager.ResolveTheme(expected, provider);

        Assert.Equal(expected, actual);
        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(ApplicationTheme.Light)]
    [InlineData(ApplicationTheme.Dark)]
    public void ResolveTheme_SystemUsesInjectedProvider(ApplicationTheme systemTheme)
    {
        var provider = new StubSystemThemeProvider(systemTheme);

        var actual = WinFormsThemeManager.ResolveTheme(ApplicationTheme.System, provider);

        Assert.Equal(systemTheme, actual);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void DarkPalette_HasReadableCoreColorPairs()
    {
        var palette = WinFormsThemeManager.GetPalette(
            ApplicationTheme.Dark,
            new StubSystemThemeProvider(ApplicationTheme.Light));

        Assert.True(ContrastRatio(palette.Foreground, palette.WindowBackground) >= 7d);
        Assert.True(ContrastRatio(palette.ButtonForeground, palette.ButtonBackground) >= 7d);
        Assert.True(ContrastRatio(palette.ErrorForeground, palette.ErrorBackground) >= 4.5d);
        Assert.True(ContrastRatio(palette.InfoForeground, palette.InfoBackground) >= 4.5d);
        Assert.NotEqual(palette.InputBackground, palette.Foreground);
    }

    [Fact]
    public void ApplyTheme_StylesRepresentativeControlTree()
    {
        using var form = new Form();
        using var menu = new MenuStrip();
        using var panel = new Panel();
        using var group = new GroupBox();
        using var label = new Label();
        using var textBox = new TextBox();
        using var comboBox = new ComboBox();
        using var numeric = new NumericUpDown();
        using var button = new Button();
        button.Enabled = false;
        using var checkBox = new CheckBox();
        using var radioButton = new RadioButton();
        using var progress = new ProgressBar();
        menu.Items.Add("Tools");
        group.Controls.AddRange([label, textBox, comboBox, numeric, button, checkBox, radioButton, progress]);
        panel.Controls.Add(group);
        form.Controls.AddRange([menu, panel]);

        WinFormsThemeManager.ApplyTheme(form, ApplicationTheme.Dark);
        var palette = WinFormsThemeManager.GetPalette(ApplicationTheme.Dark);

        Assert.Equal(palette.WindowBackground, form.BackColor);
        Assert.Equal(palette.WindowBackground, panel.BackColor);
        Assert.Equal(palette.InputBackground, textBox.BackColor);
        Assert.Equal(palette.InputBackground, comboBox.BackColor);
        Assert.Equal(palette.InputBackground, numeric.BackColor);
        Assert.Equal(palette.ButtonBackground, button.BackColor);
        Assert.Equal(palette.DisabledForeground, button.ForeColor);
        Assert.Equal(palette.MenuBackground, menu.BackColor);
        Assert.Equal(palette.Foreground, label.ForeColor);
        Assert.Equal(palette.ProgressBackground, progress.BackColor);
        Assert.IsType<ToolStripProfessionalRenderer>(menu.Renderer);
    }

    [Fact]
    public void UpdateDialogOptions_DefaultsPreserveNativeAppearanceAndDownloadContract()
    {
        using var httpClient = new HttpClient(new RejectingHandler());
        var client = new UpdateClient(httpClient, new UpdateClientOptions
        {
            RepositoryOwner = "owner",
            RepositoryName = "repository",
        });

        var options = new UpdateDialogOptions(
            client,
            "1.0.0",
            Path.Combine(Path.GetTempPath(), "update.zip"),
            release => AssetSelector.ByExtension(release, ".zip"));

        Assert.Null(options.Theme);
        Assert.False(options.ConfirmBeforeDownload);
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return 0.2126d * Channel(color.R) +
            0.7152d * Channel(color.G) +
            0.0722d * Channel(color.B);
    }

    private sealed class StubSystemThemeProvider(ApplicationTheme theme) : ISystemThemeProvider
    {
        public int CallCount { get; private set; }

        public ApplicationTheme GetSystemTheme()
        {
            CallCount++;
            return theme;
        }
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No network calls are permitted.");
    }
}
