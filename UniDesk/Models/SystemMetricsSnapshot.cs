namespace UniDesk.Models;

public sealed class SystemMetricsSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public double? CpuUsage { get; init; }
    public double? CpuTemperature { get; init; }
    public double? MemoryUsage { get; init; }
    public double? GpuUsage { get; init; }
    public double? GpuTemperature { get; init; }
    public double? NetworkReceivedBytesPerSecond { get; init; }
    public double? NetworkSentBytesPerSecond { get; init; }
    public string? CpuUsageSource { get; init; }
    public string? CpuUsageDeviceId { get; init; }
    public string? CpuTemperatureSource { get; init; }
    public string? CpuTemperatureDeviceId { get; init; }
    public string? GpuUsageSource { get; init; }
    public string? GpuUsageDeviceId { get; init; }
    public string? GpuTemperatureSource { get; init; }
    public string? GpuTemperatureDeviceId { get; init; }
    public HardwareMetricAvailability CpuUsageAvailability { get; init; } = HardwareMetricAvailability.ProviderUnavailable;
    public HardwareMetricAvailability CpuTemperatureAvailability { get; init; } = HardwareMetricAvailability.ProviderUnavailable;
    public HardwareMetricAvailability GpuUsageAvailability { get; init; } = HardwareMetricAvailability.ProviderUnavailable;
    public HardwareMetricAvailability GpuTemperatureAvailability { get; init; } = HardwareMetricAvailability.ProviderUnavailable;
    public string? CpuUsageReason { get; init; }
    public string? CpuTemperatureReason { get; init; }
    public string? GpuUsageReason { get; init; }
    public string? GpuTemperatureReason { get; init; }
}
