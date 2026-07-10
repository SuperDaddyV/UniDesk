namespace UniDesk.Services;

public interface INetworkMetricsReader : IDisposable
{
    NetworkMetrics Read();
}
