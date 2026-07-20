using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UpdateKit.WinForms;

/// <summary>Resolves and applies centralized light and dark palettes to WinForms control trees.</summary>
public static class WinFormsThemeManager
{
    private static readonly ThemePalette LightPalette = new(
        SystemColors.Control,
        Color.White,
        SystemColors.Window,
        SystemColors.ControlText,
        Color.FromArgb(80, 80, 80),
        SystemColors.GrayText,
        Color.FromArgb(173, 173, 173),
        SystemColors.Control,
        SystemColors.ControlText,
        SystemColors.MenuBar,
        SystemColors.MenuText,
        Color.FromArgb(0, 120, 212),
        Color.FromArgb(253, 231, 233),
        Color.FromArgb(164, 38, 44),
        SystemColors.Info,
        SystemColors.InfoText,
        Color.FromArgb(230, 230, 230),
        Color.FromArgb(0, 120, 212));

    private static readonly ThemePalette DarkPalette = new(
        Color.FromArgb(32, 32, 32),
        Color.FromArgb(45, 45, 48),
        Color.FromArgb(37, 37, 38),
        Color.FromArgb(241, 241, 241),
        Color.FromArgb(200, 200, 200),
        Color.FromArgb(150, 150, 150),
        Color.FromArgb(92, 92, 96),
        Color.FromArgb(58, 58, 61),
        Color.FromArgb(245, 245, 245),
        Color.FromArgb(37, 37, 38),
        Color.FromArgb(241, 241, 241),
        Color.FromArgb(76, 194, 255),
        Color.FromArgb(76, 29, 29),
        Color.FromArgb(255, 190, 187),
        Color.FromArgb(24, 58, 74),
        Color.FromArgb(216, 243, 255),
        Color.FromArgb(55, 55, 58),
        Color.FromArgb(76, 194, 255));

    /// <summary>Resolves System to the current Windows application theme.</summary>
    public static ApplicationTheme ResolveTheme(ApplicationTheme theme) =>
        ResolveTheme(theme, WindowsSystemThemeProvider.Instance);

    /// <summary>Gets the palette for an explicit or system-resolved theme.</summary>
    public static ThemePalette GetPalette(ApplicationTheme theme) =>
        GetPalette(theme, WindowsSystemThemeProvider.Instance);

    /// <summary>Applies the requested theme recursively to a form or control tree.</summary>
    public static void ApplyTheme(Control root, ApplicationTheme theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        var resolvedTheme = ResolveTheme(theme);
        var palette = resolvedTheme == ApplicationTheme.Dark ? DarkPalette : LightPalette;
        ApplyControl(root, palette);

        if (root is Form form && form.IsHandleCreated)
        {
            ApplyWindowChrome(form, resolvedTheme == ApplicationTheme.Dark);
        }
    }

    /// <summary>Applies informational colors from the requested theme.</summary>
    public static void ApplyInformationStyle(Control control, ApplicationTheme theme)
    {
        ArgumentNullException.ThrowIfNull(control);
        var palette = GetPalette(theme);
        control.BackColor = palette.InfoBackground;
        control.ForeColor = palette.InfoForeground;
    }

    /// <summary>Applies error colors from the requested theme.</summary>
    public static void ApplyErrorStyle(Control control, ApplicationTheme theme)
    {
        ArgumentNullException.ThrowIfNull(control);
        var palette = GetPalette(theme);
        control.BackColor = palette.ErrorBackground;
        control.ForeColor = palette.ErrorForeground;
    }

    internal static ApplicationTheme ResolveTheme(
        ApplicationTheme theme,
        ISystemThemeProvider systemThemeProvider)
    {
        ArgumentNullException.ThrowIfNull(systemThemeProvider);
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        return theme == ApplicationTheme.System
            ? systemThemeProvider.GetSystemTheme()
            : theme;
    }

    internal static ThemePalette GetPalette(
        ApplicationTheme theme,
        ISystemThemeProvider systemThemeProvider) =>
        ResolveTheme(theme, systemThemeProvider) == ApplicationTheme.Dark
            ? DarkPalette
            : LightPalette;

    internal static void ApplyWindowChrome(Form form, bool useDarkMode)
    {
        if (!OperatingSystem.IsWindows() || !form.IsHandleCreated)
        {
            return;
        }

        var enabled = useDarkMode ? 1 : 0;
        if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
        }
    }

    private static void ApplyControl(Control control, ThemePalette palette)
    {
        control.ForeColor = control.Enabled ? palette.Foreground : palette.DisabledForeground;

        switch (control)
        {
            case TextBoxBase textBox:
                textBox.BackColor = palette.InputBackground;
                textBox.ForeColor = control.Enabled ? palette.Foreground : palette.DisabledForeground;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = palette.InputBackground;
                comboBox.ForeColor = control.Enabled ? palette.Foreground : palette.DisabledForeground;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = palette.InputBackground;
                numericUpDown.ForeColor = control.Enabled ? palette.Foreground : palette.DisabledForeground;
                numericUpDown.BorderStyle = BorderStyle.FixedSingle;
                break;

            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = palette.ButtonBackground;
                button.ForeColor = control.Enabled ? palette.ButtonForeground : palette.DisabledForeground;
                button.FlatAppearance.BorderColor = palette.Border;
                button.FlatAppearance.MouseOverBackColor = Blend(palette.ButtonBackground, palette.Accent, 0.18);
                button.FlatAppearance.MouseDownBackColor = Blend(palette.ButtonBackground, palette.Accent, 0.30);
                break;

            case CheckBox checkBox:
                checkBox.UseVisualStyleBackColor = false;
                checkBox.BackColor = Color.Transparent;
                break;

            case RadioButton radioButton:
                radioButton.UseVisualStyleBackColor = false;
                radioButton.BackColor = Color.Transparent;
                break;

            case MenuStrip menuStrip:
                menuStrip.BackColor = palette.MenuBackground;
                menuStrip.ForeColor = palette.MenuForeground;
                menuStrip.Renderer = new ToolStripProfessionalRenderer(
                    new ThemeColorTable(palette));
                ApplyToolStripItems(menuStrip.Items, palette);
                break;

            case ProgressBar progressBar:
                progressBar.BackColor = palette.ProgressBackground;
                progressBar.ForeColor = palette.ProgressForeground;
                if (OperatingSystem.IsWindows() && progressBar.IsHandleCreated)
                {
                    SendMessage(progressBar.Handle, 0x0409, IntPtr.Zero, ColorRef(palette.ProgressBackground));
                    SendMessage(progressBar.Handle, 0x0410, IntPtr.Zero, ColorRef(palette.ProgressForeground));
                }
                break;

            case Form or Panel or TableLayoutPanel or FlowLayoutPanel or UserControl:
                control.BackColor = palette.WindowBackground;
                break;

            case GroupBox:
                control.BackColor = palette.WindowBackground;
                break;

            case Label or LinkLabel:
                control.BackColor = Color.Transparent;
                break;

            default:
                control.BackColor = palette.WindowBackground;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControl(child, palette);
        }
    }

    private static void ApplyToolStripItems(
        ToolStripItemCollection items,
        ThemePalette palette)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = palette.MenuBackground;
            item.ForeColor = item.Enabled ? palette.MenuForeground : palette.DisabledForeground;
            if (item is ToolStripDropDownItem dropDownItem)
            {
                dropDownItem.DropDown.BackColor = palette.MenuBackground;
                dropDownItem.DropDown.ForeColor = palette.MenuForeground;
                ApplyToolStripItems(dropDownItem.DropDownItems, palette);
            }
        }
    }

    private static Color Blend(Color first, Color second, double secondWeight)
    {
        var firstWeight = 1d - secondWeight;
        return Color.FromArgb(
            (int)Math.Round(first.R * firstWeight + second.R * secondWeight),
            (int)Math.Round(first.G * firstWeight + second.G * secondWeight),
            (int)Math.Round(first.B * firstWeight + second.B * secondWeight));
    }

    private static IntPtr ColorRef(Color color) =>
        new(color.R | color.G << 8 | color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter);

    private sealed class ThemeColorTable : ProfessionalColorTable
    {
        private readonly ThemePalette _palette;

        public ThemeColorTable(ThemePalette palette)
        {
            _palette = palette;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => _palette.MenuBackground;
        public override Color MenuStripGradientEnd => _palette.MenuBackground;
        public override Color ToolStripDropDownBackground => _palette.MenuBackground;
        public override Color ImageMarginGradientBegin => _palette.MenuBackground;
        public override Color ImageMarginGradientMiddle => _palette.MenuBackground;
        public override Color ImageMarginGradientEnd => _palette.MenuBackground;
        public override Color MenuItemSelected => Blend(_palette.MenuBackground, _palette.Accent, 0.20);
        public override Color MenuItemBorder => _palette.Border;
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => MenuItemSelected;
        public override Color MenuItemPressedGradientMiddle => MenuItemSelected;
        public override Color MenuItemPressedGradientEnd => MenuItemSelected;
        public override Color SeparatorDark => _palette.Border;
        public override Color SeparatorLight => _palette.Border;
    }
}

internal interface ISystemThemeProvider
{
    ApplicationTheme GetSystemTheme();
}

internal sealed class WindowsSystemThemeProvider : ISystemThemeProvider
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static WindowsSystemThemeProvider Instance { get; } = new();

    private WindowsSystemThemeProvider()
    {
    }

    public ApplicationTheme GetSystemTheme()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ApplicationTheme.Light;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return ApplicationTheme.Light;
        }
    }
}
