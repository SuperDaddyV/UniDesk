namespace UniDesk.Services;

public readonly record struct NetworkMetrics(
    double? ReceivedBytesPerSecond,
    double? SentBytesPerSecond)
{
    public static readonly NetworkMetrics Empty = new(null, null);
    public static readonly NetworkMetrics Zero = new(0, 0);
}

public sealed record NetworkSample(
    DateTimeOffset Timestamp,
    double ReceivedBytes,
    double SentBytes);
