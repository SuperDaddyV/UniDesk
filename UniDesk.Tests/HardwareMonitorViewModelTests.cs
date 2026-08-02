using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class HardwareMonitorViewModelTests
{
    [Fact]
    public void Snapshot_ShouldFormatAllDisplayedValues()
    {
        var monitor = new FakeMonitor();
        using var viewModel = new HardwareMonitorViewModel(monitor);

        monitor.Raise(new SystemMetricsSnapshot
        {
            CpuUsage = 42.4,
            CpuTemperature = 61.2,
            MemoryUsage = 75.5,
            GpuUsage = 33.6,
            GpuTemperature = 70.8,
            NetworkReceivedBytesPerSecond = 2 * 1024 * 1024,
            NetworkSentBytesPerSecond = 512 * 1024
        });

        Assert.Equal("42%", viewModel.SystemCpuUsageText);
        Assert.Equal("CPU 61℃", viewModel.SystemCpuTemperatureText);
        Assert.Equal("61℃", viewModel.SystemCpuTemperatureValueText);
        Assert.Equal("76%", viewModel.SystemMemoryUsageText);
        Assert.Equal("34%", viewModel.SystemGpuUsageText);
        Assert.Equal("GPU 71℃", viewModel.SystemGpuTemperatureText);
        Assert.Equal("71℃", viewModel.SystemGpuTemperatureValueText);
        Assert.Equal("2.0 MB/s", viewModel.SystemNetworkReceivedText);
        Assert.Equal("512 KB/s", viewModel.SystemNetworkSentText);
    }

    [Fact]
    public void EmptySnapshot_ShouldUseUnavailableFormatting()
    {
        var monitor = new FakeMonitor();
        using var viewModel = new HardwareMonitorViewModel(monitor);

        monitor.Raise(new SystemMetricsSnapshot());

        Assert.Equal("--", viewModel.SystemCpuUsageText);
        Assert.Equal("CPU --℃", viewModel.SystemCpuTemperatureText);
        Assert.Equal("--℃", viewModel.SystemCpuTemperatureValueText);
        Assert.Equal("--", viewModel.SystemMemoryUsageText);
        Assert.Equal("--", viewModel.SystemGpuUsageText);
        Assert.Equal("GPU --℃", viewModel.SystemGpuTemperatureText);
        Assert.Equal("--℃", viewModel.SystemGpuTemperatureValueText);
        Assert.Equal("--", viewModel.SystemNetworkReceivedText);
        Assert.Equal("--", viewModel.SystemNetworkSentText);
    }

    [Fact]
    public void Snapshot_ShouldExposeSourceAndAvailabilityTooltips()
    {
        var monitor = new FakeMonitor();
        using var viewModel = new HardwareMonitorViewModel(monitor);

        monitor.Raise(new SystemMetricsSnapshot
        {
            CapturedAtUtc = DateTimeOffset.Parse("2026-07-12T00:00:00Z"),
            CpuUsage = 42,
            CpuUsageSource = "Windows Performance Counter",
            CpuUsageAvailability = HardwareMetricAvailability.Available,
            CpuTemperatureAvailability = HardwareMetricAvailability.NoSensor,
            GpuUsageAvailability = HardwareMetricAvailability.Stale,
            GpuTemperatureAvailability = HardwareMetricAvailability.NeedsElevation
        });

        Assert.Contains("Windows Performance Counter", viewModel.SystemCpuUsageToolTip, StringComparison.Ordinal);
        Assert.Contains("No sensor", viewModel.SystemCpuTemperatureToolTip, StringComparison.Ordinal);
        Assert.Contains("stale", viewModel.SystemGpuUsageToolTip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("elevation", viewModel.SystemGpuTemperatureToolTip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_ShouldStopAndSuppressFurtherUpdates()
    {
        var monitor = new FakeMonitor();
        var viewModel = new HardwareMonitorViewModel(monitor);
        monitor.Raise(new SystemMetricsSnapshot { CpuUsage = 10 });

        viewModel.Dispose();
        monitor.Raise(new SystemMetricsSnapshot { CpuUsage = 90 });

        Assert.Equal("10%", viewModel.SystemCpuUsageText);
        Assert.True(monitor.Disposed);
    }

    private sealed class FakeMonitor : ISystemMetricsMonitor
    {
        public event EventHandler<SystemMetricsSnapshot>? SnapshotAvailable;
        public bool IsEnabled { get; set; } = true;
        public bool Disposed { get; private set; }
        public void Start() { }
        public void Stop() { }
        public void Dispose() => Disposed = true;
        public void Raise(SystemMetricsSnapshot snapshot) => SnapshotAvailable?.Invoke(this, snapshot);
    }
}
