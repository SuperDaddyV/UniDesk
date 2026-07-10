namespace UniDesk.Services;

public interface ICpuMetricsReader : IDisposable
{
    CpuMetrics Read();
}
