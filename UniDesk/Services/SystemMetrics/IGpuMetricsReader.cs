namespace UniDesk.Services;

public interface IGpuMetricsReader : IDisposable
{
    GpuMetrics Read();
}
