using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class SystemMetricsMonitorTests
{
    [Fact]
    public async Task Monitor_ShouldNeverOverlapReads()
    {
        var reader = new BlockingMetricsReader();
        using var monitor = new SystemMetricsMonitor(
            reader,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(1));

        monitor.Start();
        await reader.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(80);

        Assert.Equal(1, reader.MaxConcurrentReads);
        reader.ReleaseFirstRead.Set();
    }

    [Fact]
    public async Task Dispose_ShouldSuppressLateSnapshot()
    {
        var reader = new BlockingMetricsReader();
        var snapshots = 0;
        var monitor = new SystemMetricsMonitor(
            reader,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(1));
        monitor.SnapshotAvailable += (_, _) => Interlocked.Increment(ref snapshots);
        monitor.Start();
        await reader.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        monitor.Dispose();
        reader.ReleaseFirstRead.Set();
        await Task.Delay(80);

        Assert.Equal(0, snapshots);
    }

    [Fact]
    public async Task DisabledMonitor_ShouldNotReadUntilEnabled()
    {
        var reader = new CountingMetricsReader();
        using var monitor = new SystemMetricsMonitor(
            reader,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(1))
        {
            IsEnabled = false
        };

        monitor.Start();
        await Task.Delay(60);
        Assert.Equal(0, reader.ReadCount);

        monitor.IsEnabled = true;
        await reader.ReadObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(reader.ReadCount > 0);
    }

    private sealed class BlockingMetricsReader : ISystemMetricsService
    {
        private int _activeReads;
        private int _maxConcurrentReads;

        public int MaxConcurrentReads => Volatile.Read(ref _maxConcurrentReads);
        public TaskCompletionSource<bool> FirstReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseFirstRead { get; } = new(false);

        public SystemMetricsSnapshot Read()
        {
            var active = Interlocked.Increment(ref _activeReads);
            UpdateMaximum(active);
            FirstReadStarted.TrySetResult(true);
            ReleaseFirstRead.Wait();
            Interlocked.Decrement(ref _activeReads);
            return new SystemMetricsSnapshot { CpuUsage = 42 };
        }

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrentReads);
                if (active <= current ||
                    Interlocked.CompareExchange(ref _maxConcurrentReads, active, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class CountingMetricsReader : ISystemMetricsService
    {
        private int _readCount;
        public int ReadCount => Volatile.Read(ref _readCount);
        public TaskCompletionSource<bool> ReadObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SystemMetricsSnapshot Read()
        {
            Interlocked.Increment(ref _readCount);
            ReadObserved.TrySetResult(true);
            return new SystemMetricsSnapshot();
        }
    }
}
