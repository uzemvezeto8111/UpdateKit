using System.Drawing;

namespace UpdateKit.WinForms;

/// <summary>Provides centralized colors used by themed UpdateKit WinForms surfaces.</summary>
public sealed class ThemePalette
{
    internal ThemePalette(
        Color windowBackground,
        Color surfaceBackground,
        Color inputBackground,
        Color foreground,
        Color secondaryForeground,
        Color disabledForeground,
        Color border,
        Color buttonBackground,
        Color buttonForeground,
        Color menuBackground,
        Color menuForeground,
        Color accent,
        Color errorBackground,
        Color errorForeground,
        Color infoBackground,
        Color infoForeground,
        Color progressBackground,
        Color progressForeground)
    {
        WindowBackground = windowBackground;
        SurfaceBackground = surfaceBackground;
        InputBackground = inputBackground;
        Foreground = foreground;
        SecondaryForeground = secondaryForeground;
        DisabledForeground = disabledForeground;
        Border = border;
        ButtonBackground = buttonBackground;
        ButtonForeground = buttonForeground;
        MenuBackground = menuBackground;
        MenuForeground = menuForeground;
        Accent = accent;
        ErrorBackground = errorBackground;
        ErrorForeground = errorForeground;
        InfoBackground = infoBackground;
        InfoForeground = infoForeground;
        ProgressBackground = progressBackground;
        ProgressForeground = progressForeground;
    }

    /// <summary>Gets the primary form and layout background.</summary>
    public Color WindowBackground { get; }

    /// <summary>Gets the elevated surface background.</summary>
    public Color SurfaceBackground { get; }

    /// <summary>Gets the editable and read-only text input background.</summary>
    public Color InputBackground { get; }

    /// <summary>Gets the primary foreground color.</summary>
    public Color Foreground { get; }

    /// <summary>Gets the secondary foreground color.</summary>
    public Color SecondaryForeground { get; }

    /// <summary>Gets the foreground color intended for disabled content.</summary>
    public Color DisabledForeground { get; }

    /// <summary>Gets the control-border color.</summary>
    public Color Border { get; }

    /// <summary>Gets the standard button background.</summary>
    public Color ButtonBackground { get; }

    /// <summary>Gets the standard button foreground.</summary>
    public Color ButtonForeground { get; }

    /// <summary>Gets the menu-strip background.</summary>
    public Color MenuBackground { get; }

    /// <summary>Gets the menu-strip foreground.</summary>
    public Color MenuForeground { get; }

    /// <summary>Gets the focus and progress accent color.</summary>
    public Color Accent { get; }

    /// <summary>Gets the error presentation background.</summary>
    public Color ErrorBackground { get; }

    /// <summary>Gets the error presentation foreground.</summary>
    public Color ErrorForeground { get; }

    /// <summary>Gets the informational presentation background.</summary>
    public Color InfoBackground { get; }

    /// <summary>Gets the informational presentation foreground.</summary>
    public Color InfoForeground { get; }

    /// <summary>Gets the progress-track background.</summary>
    public Color ProgressBackground { get; }

    /// <summary>Gets the completed-progress foreground.</summary>
    public Color ProgressForeground { get; }
}
