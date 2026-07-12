using UniDesk.Models;

namespace UniDesk.Services;

public readonly struct GpuMetrics
{
    public static readonly GpuMetrics Empty = new(null, null, "None", 1000, false);

    public GpuMetrics(
        double? gpuUsage,
        double? gpuTemperature,
        string sourceName = "Unknown",
        int sourcePriority = 100,
        bool isDiscrete = true,
        string? usageSource = null,
        string? usageDeviceId = null,
        string? temperatureSource = null,
        string? temperatureDeviceId = null,
        HardwareMetricAvailability? usageAvailability = null,
        HardwareMetricAvailability? temperatureAvailability = null,
        string? usageReason = null,
        string? temperatureReason = null)
    {
        GpuUsage = gpuUsage;
        GpuTemperature = gpuTemperature;
        SourceName = sourceName;
        SourcePriority = sourcePriority;
        IsDiscrete = isDiscrete;
        UsageSource = usageSource ?? (gpuUsage.HasValue ? sourceName : null);
        UsageDeviceId = usageDeviceId;
        TemperatureSource = temperatureSource ?? (gpuTemperature.HasValue ? sourceName : null);
        TemperatureDeviceId = temperatureDeviceId;
        UsageAvailability = usageAvailability ?? InferAvailability(gpuUsage);
        TemperatureAvailability = temperatureAvailability ?? InferAvailability(gpuTemperature);
        UsageReason = usageReason;
        TemperatureReason = temperatureReason;
    }

    public double? GpuUsage { get; }
    public double? GpuTemperature { get; }
    public string SourceName { get; }
    public int SourcePriority { get; }
    public bool IsDiscrete { get; }
    public string? UsageSource { get; }
    public string? UsageDeviceId { get; }
    public string? TemperatureSource { get; }
    public string? TemperatureDeviceId { get; }
    public HardwareMetricAvailability UsageAvailability { get; }
    public HardwareMetricAvailability TemperatureAvailability { get; }
    public string? UsageReason { get; }
    public string? TemperatureReason { get; }
    public bool HasAnyValue => GpuUsage.HasValue || GpuTemperature.HasValue;
    public bool HasAllValues => GpuUsage.HasValue && GpuTemperature.HasValue;
    public int SelectionRank => (HasAllValues, IsDiscrete) switch
    {
        (true, true) => 0,
        (true, false) => 1,
        (false, true) => 2,
        (false, false) => 3
    };

    private static HardwareMetricAvailability InferAvailability(double? value) =>
        value.HasValue
            ? HardwareMetricAvailability.Available
            : HardwareMetricAvailability.ProviderUnavailable;
}
