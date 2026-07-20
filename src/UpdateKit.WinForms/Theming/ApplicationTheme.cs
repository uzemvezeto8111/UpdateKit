namespace UpdateKit.WinForms;

/// <summary>Specifies how UpdateKit WinForms surfaces choose their colors.</summary>
public enum ApplicationTheme
{
    /// <summary>Uses the current Windows application theme.</summary>
    System,

    /// <summary>Uses UpdateKit's explicit light palette.</summary>
    Light,

    /// <summary>Uses UpdateKit's explicit dark palette.</summary>
    Dark,
}
