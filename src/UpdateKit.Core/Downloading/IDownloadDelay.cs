namespace UpdateKit;

internal interface IDownloadDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class SystemDownloadDelay : IDownloadDelay
{
    public static SystemDownloadDelay Instance { get; } = new();

    private SystemDownloadDelay()
    {
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
