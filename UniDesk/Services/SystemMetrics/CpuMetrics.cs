using UniDesk.Models;

namespace UniDesk.Services;

public readonly struct CpuMetrics
{
    public static readonly CpuMetrics Empty = new(null, null);

    public CpuMetrics(
        double? cpuUsage,
        double? cpuTemperature,
        string? usageSource = null,
        string? usageDeviceId = null,
        string? temperatureSource = null,
        string? temperatureDeviceId = null,
        HardwareMetricAvailability? usageAvailability = null,
        HardwareMetricAvailability? temperatureAvailability = null,
        string? usageReason = null,
        string? temperatureReason = null)
    {
        CpuUsage = cpuUsage;
        CpuTemperature = cpuTemperature;
        UsageSource = usageSource;
        UsageDeviceId = usageDeviceId;
        TemperatureSource = temperatureSource;
        TemperatureDeviceId = temperatureDeviceId;
        UsageAvailability = usageAvailability ?? InferAvailability(cpuUsage);
        TemperatureAvailability = temperatureAvailability ?? InferAvailability(cpuTemperature);
        UsageReason = usageReason;
        TemperatureReason = temperatureReason;
    }

    public double? CpuUsage { get; }
    public double? CpuTemperature { get; }
    public string? UsageSource { get; }
    public string? UsageDeviceId { get; }
    public string? TemperatureSource { get; }
    public string? TemperatureDeviceId { get; }
    public HardwareMetricAvailability UsageAvailability { get; }
    public HardwareMetricAvailability TemperatureAvailability { get; }
    public string? UsageReason { get; }
    public string? TemperatureReason { get; }

    private static HardwareMetricAvailability InferAvailability(double? value) =>
        value.HasValue
            ? HardwareMetricAvailability.Available
            : HardwareMetricAvailability.ProviderUnavailable;
}
