namespace UpdateKit.Example.WinForms.Services;

internal sealed class AutomaticUpdateCheckCoordinator
{
    private int _started;

    public bool TryBegin(bool enabled, bool configurationIsValid) =>
        enabled && configurationIsValid && Interlocked.CompareExchange(ref _started, 1, 0) == 0;
}
