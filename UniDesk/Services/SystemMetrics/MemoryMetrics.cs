namespace UniDesk.Services;

public readonly record struct MemoryMetrics(
    double? UsagePercent,
    ulong TotalBytes,
    ulong AvailableBytes,
    ulong UsedBytes)
{
    public static readonly MemoryMetrics Empty = new(null, 0, 0, 0);
}
