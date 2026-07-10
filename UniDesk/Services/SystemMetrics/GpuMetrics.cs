
namespace UniDesk.Services;

public readonly struct GpuMetrics
{
    public static readonly GpuMetrics Empty = new(null, null, "None", 1000, false);

    public GpuMetrics(
        double? gpuUsage,
        double? gpuTemperature,
        string sourceName = "Unknown",
        int sourcePriority = 100,
        bool isDiscrete = true)
    {
        GpuUsage = gpuUsage;
        GpuTemperature = gpuTemperature;
        SourceName = sourceName;
        SourcePriority = sourcePriority;
        IsDiscrete = isDiscrete;
    }

    public double? GpuUsage { get; }
    public double? GpuTemperature { get; }
    public string SourceName { get; }
    public int SourcePriority { get; }
    public bool IsDiscrete { get; }
    public bool HasAnyValue => GpuUsage.HasValue || GpuTemperature.HasValue;
    public bool HasAllValues => GpuUsage.HasValue && GpuTemperature.HasValue;
    public int SelectionRank => (HasAllValues, IsDiscrete) switch
    {
        (true, true) => 0,
        (true, false) => 1,
        (false, true) => 2,
        (false, false) => 3
    };
}
