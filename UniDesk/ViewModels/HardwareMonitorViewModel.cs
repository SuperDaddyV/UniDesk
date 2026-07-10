using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class HardwareMonitorViewModel : ObservableObject, IDisposable
{
    private readonly ISystemMetricsMonitor _monitor;
    private bool _disposed;

    [ObservableProperty]
    private string _systemCpuUsageText = "--";

    [ObservableProperty]
    private string _systemCpuTemperatureText = "CPU --";

    [ObservableProperty]
    private string _systemMemoryUsageText = "--";

    [ObservableProperty]
    private string _systemGpuUsageText = "--";

    [ObservableProperty]
    private string _systemGpuTemperatureText = "GPU --";

    [ObservableProperty]
    private string _systemNetworkReceivedText = "--";

    [ObservableProperty]
    private string _systemNetworkSentText = "--";

    public HardwareMonitorViewModel(ISystemMetricsMonitor monitor)
    {
        _monitor = monitor;
        _monitor.SnapshotAvailable += Monitor_OnSnapshotAvailable;
        _monitor.Start();
    }

    public bool IsEnabled
    {
        get => _monitor.IsEnabled;
        set => _monitor.IsEnabled = value;
    }

    private void Monitor_OnSnapshotAvailable(object? sender, SystemMetricsSnapshot metrics)
    {
        if (_disposed || !_monitor.IsEnabled) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            Apply(metrics);
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && _monitor.IsEnabled)
            {
                Apply(metrics);
            }
        });
    }

    private void Apply(SystemMetricsSnapshot metrics)
    {
        SystemCpuUsageText = FormatPercent(metrics.CpuUsage);
        SystemCpuTemperatureText = FormatTemperature("CPU", metrics.CpuTemperature);
        SystemMemoryUsageText = FormatPercent(metrics.MemoryUsage);
        SystemGpuUsageText = FormatPercent(metrics.GpuUsage);
        SystemGpuTemperatureText = FormatTemperature("GPU", metrics.GpuTemperature);
        SystemNetworkReceivedText = FormatSpeed(metrics.NetworkReceivedBytesPerSecond);
        SystemNetworkSentText = FormatSpeed(metrics.NetworkSentBytesPerSecond);
    }

    private static string FormatPercent(double? value) => value.HasValue ? $"{value.Value:0}%" : "--";

    private static string FormatTemperature(string label, double? value) =>
        value.HasValue ? $"{label} {value.Value:0}℃" : $"{label} --℃";

    private static string FormatSpeed(double? bytesPerSecond)
    {
        if (!bytesPerSecond.HasValue) return "--";

        var value = Math.Max(0, bytesPerSecond.Value);
        if (value < 1) return "0 B/s";
        if (value < 1024) return $"{value:0} B/s";

        value /= 1024;
        if (value < 1024) return $"{value:0} KB/s";

        value /= 1024;
        if (value < 1024) return $"{value:0.0} MB/s";

        value /= 1024;
        return $"{value:0.0} GB/s";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.SnapshotAvailable -= Monitor_OnSnapshotAvailable;
        _monitor.Dispose();
    }
}
