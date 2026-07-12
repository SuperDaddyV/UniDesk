namespace UniDesk.Services;

public sealed class ReaderFailureBackoff
{
    private const int FailureThreshold = 3;
    private readonly object _sync = new();
    private readonly TimeSpan _retryDelay;
    private readonly Func<DateTimeOffset> _utcNow;
    private int _consecutiveFailures;
    private DateTimeOffset? _nextRetryAtUtc;
    private bool _isPermanentlyUnavailable;
    private string? _lastFailureReason;

    public ReaderFailureBackoff(TimeSpan retryDelay, Func<DateTimeOffset>? utcNow = null)
    {
        if (retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        _retryDelay = retryDelay;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool CanAttempt
    {
        get
        {
            lock (_sync)
            {
                return !_isPermanentlyUnavailable &&
                       (!_nextRetryAtUtc.HasValue || _utcNow() >= _nextRetryAtUtc.Value);
            }
        }
    }

    public int ConsecutiveFailures
    {
        get
        {
            lock (_sync)
            {
                return _consecutiveFailures;
            }
        }
    }

    public DateTimeOffset? NextRetryAtUtc
    {
        get
        {
            lock (_sync)
            {
                return _nextRetryAtUtc;
            }
        }
    }

    public bool IsPermanentlyUnavailable
    {
        get
        {
            lock (_sync)
            {
                return _isPermanentlyUnavailable;
            }
        }
    }

    public string? LastFailureReason
    {
        get
        {
            lock (_sync)
            {
                return _lastFailureReason;
            }
        }
    }

    public void RecordFailure(string reason)
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            _lastFailureReason = reason;
            if (_consecutiveFailures >= FailureThreshold)
            {
                _nextRetryAtUtc = _utcNow().Add(_retryDelay);
            }
        }
    }

    public void RecordPermanentFailure(string reason)
    {
        lock (_sync)
        {
            _isPermanentlyUnavailable = true;
            _lastFailureReason = reason;
            _nextRetryAtUtc = null;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _nextRetryAtUtc = null;
            _lastFailureReason = null;
        }
    }
}
