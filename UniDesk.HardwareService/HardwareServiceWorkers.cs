namespace UniDesk.HardwareService;

public sealed class HardwareSensorWorker : BackgroundService
{
    private readonly LibreHardwareSnapshotCollector _collector;
    private readonly HardwareSnapshotState _state;
    private readonly ILogger<HardwareSensorWorker> _logger;

    public HardwareSensorWorker(
        LibreHardwareSnapshotCollector collector,
        HardwareSnapshotState state,
        ILogger<HardwareSensorWorker> logger)
    {
        _collector = collector;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _state.Update(_collector.Collect());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hardware sensor refresh failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _collector.Dispose();
        base.Dispose();
    }
}

public sealed class HardwarePipeWorker : BackgroundService
{
    private readonly HardwarePipeServer _server;
    private readonly ILogger<HardwarePipeWorker> _logger;

    public HardwarePipeWorker(
        HardwarePipeServer server,
        ILogger<HardwarePipeWorker> logger)
    {
        _server = server;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _server.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Hardware service pipe server stopped unexpectedly.");
            throw;
        }
    }
}
