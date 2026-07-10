using System.Diagnostics;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class SystemMetricsMonitor : ISystemMetricsMonitor
{
    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(1);

    private readonly ISystemMetricsService _reader;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _slowThreshold;
    private readonly bool _ownsReader;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private DateTime _lastErrorWarningUtc = DateTime.MinValue;
    private DateTime _lastSlowWarningUtc = DateTime.MinValue;
    private int _isEnabled = 1;
    private bool _disposed;

    public SystemMetricsMonitor(
        ISystemMetricsService reader,
        TimeSpan interval,
        TimeSpan slowThreshold,
        bool ownsReader = false)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (slowThreshold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(slowThreshold));

        _reader = reader;
        _interval = interval;
        _slowThreshold = slowThreshold;
        _ownsReader = ownsReader;
    }

    public event EventHandler<SystemMetricsSnapshot>? SnapshotAvailable;

    public bool IsEnabled
    {
        get => Volatile.Read(ref _isEnabled) == 1;
        set => Volatile.Write(ref _isEnabled, value ? 1 : 0);
    }

    public void Start()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loopTask is { IsCompleted: false }) return;

            _loopCts?.Dispose();
            var cts = new CancellationTokenSource();
            _loopCts = cts;
            _loopTask = Task.Run(() => RunAsync(cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_stateLock)
        {
            cts = _loopCts;
        }

        cts?.Cancel();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsEnabled)
            {
                if (!await DelayAsync(cancellationToken)) return;
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var snapshot = _reader.Read();
                stopwatch.Stop();
                LogSlowSample(stopwatch.Elapsed);
                if (!cancellationToken.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        SnapshotAvailable?.Invoke(this, snapshot);
                    }
                    catch (Exception ex)
                    {
                        LogReaderError(ex, "SystemMetricsMonitor.SnapshotAvailable");
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogReaderError(ex, "SystemMetricsMonitor.Read");
            }

            if (!await DelayAsync(cancellationToken)) return;
        }
    }

    private async Task<bool> DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void LogSlowSample(TimeSpan elapsed)
    {
        if (elapsed <= _slowThreshold) return;
        var now = DateTime.UtcNow;
        if (now - _lastSlowWarningUtc < WarningInterval) return;

        _lastSlowWarningUtc = now;
        Logger.LogWarning(
            $"系统指标读取耗时 {elapsed.TotalMilliseconds:0} ms。后续采样保持串行。",
            "SystemMetricsMonitor.SlowRead");
    }

    private void LogReaderError(Exception ex, string source)
    {
        var now = DateTime.UtcNow;
        if (now - _lastErrorWarningUtc < WarningInterval) return;

        _lastErrorWarningUtc = now;
        Logger.LogError(ex, source);
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;
        Task? loopTask;
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            cts = _loopCts;
            loopTask = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        cts?.Cancel();
        if (loopTask == null)
        {
            cts?.Dispose();
            DisposeOwnedReader();
            return;
        }

        _ = loopTask.ContinueWith(
            _ =>
            {
                cts?.Dispose();
                DisposeOwnedReader();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void DisposeOwnedReader()
    {
        if (_ownsReader && _reader is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
