namespace UniDesk.HardwareService;

public sealed class InitializationRetryPolicy
{
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maximumDelay;
    private int _failureCount;
    private DateTimeOffset _nextAttemptUtc = DateTimeOffset.MinValue;

    public InitializationRetryPolicy(TimeSpan initialDelay, TimeSpan maximumDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, initialDelay);
        _initialDelay = initialDelay;
        _maximumDelay = maximumDelay;
    }

    public bool CanAttempt(DateTimeOffset nowUtc) => nowUtc >= _nextAttemptUtc;

    public void RecordFailure(DateTimeOffset nowUtc)
    {
        _failureCount = Math.Min(_failureCount + 1, 31);
        var multiplier = Math.Pow(2, _failureCount - 1);
        var delayTicks = Math.Min(
            _initialDelay.Ticks * multiplier,
            _maximumDelay.Ticks);
        _nextAttemptUtc = nowUtc.AddTicks((long)delayTicks);
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _nextAttemptUtc = DateTimeOffset.MinValue;
    }
}
