using UniDesk.Models;

namespace UniDesk.Services;

public interface ISensorDiagnosticsService
{
    Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public interface IHardwareMetricsDiagnosticsSource
{
    HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics();
}

public sealed record HardwareMetricsDiagnosticsSnapshot(
    DateTimeOffset CapturedAtUtc,
    LibreHardwareHostDiagnosticStatus LibreHardwareStatus,
    GpuEngineReaderDiagnosticStatus? GpuEngineStatus,
    IReadOnlyList<HardwareSensorSnapshot> Sensors,
    IReadOnlyList<SystemMetricsSnapshot> RecentSnapshots);
