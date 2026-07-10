using UniDesk.Models;

namespace UniDesk.Services;

public interface ISystemMetricsMonitor : IDisposable
{
    event EventHandler<SystemMetricsSnapshot>? SnapshotAvailable;
    bool IsEnabled { get; set; }
    void Start();
    void Stop();
}
