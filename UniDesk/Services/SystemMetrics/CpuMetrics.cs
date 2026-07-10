namespace UniDesk.Services;

public readonly record struct CpuMetrics(double? CpuUsage, double? CpuTemperature)
{
    public static readonly CpuMetrics Empty = new(null, null);
}
