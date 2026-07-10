namespace UniDesk.Helpers;

public sealed class FatalExceptionCoordinator
{
    private int _shutdownStarted;

    public bool TryBeginShutdown() =>
        Interlocked.Exchange(ref _shutdownStarted, 1) == 0;
}
