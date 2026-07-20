namespace UpdateKit.Wpf;

/// <summary>Identifies the current state of an update-window workflow.</summary>
public enum UpdateWindowStatus
{
    /// <summary>The window is ready to check for updates.</summary>
    Initial,
    /// <summary>The client is retrieving and comparing releases.</summary>
    Checking,
    /// <summary>The installed version is current.</summary>
    NoUpdate,
    /// <summary>A newer release and matching asset are available.</summary>
    UpdateAvailable,
    /// <summary>The selected asset is being downloaded or verified.</summary>
    Downloading,
    /// <summary>Cancellation has been requested and is settling.</summary>
    Canceling,
    /// <summary>The download and any configured verification succeeded.</summary>
    Succeeded,
    /// <summary>The operation was canceled safely.</summary>
    Canceled,
    /// <summary>The operation failed.</summary>
    Failed,
}
