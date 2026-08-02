using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class HardwareMonitorViewModel : ObservableObject, IDisposable
{
    private readonly ISystemMetricsMonitor _monitor;
    private readonly ILocalizationService? _localizationService;
    private SystemMetricsSnapshot? _lastSnapshot;
    private bool _disposed;

    [ObservableProperty]
    private string _systemCpuUsageText = "--";

    [ObservableProperty]
    private string _systemCpuTemperatureText = "CPU --℃";

    [ObservableProperty]
    private string _systemCpuTemperatureValueText = "--℃";

    [ObservableProperty]
    private string _systemMemoryUsageText = "--";

    [ObservableProperty]
    private string _systemGpuUsageText = "--";

    [ObservableProperty]
    private string _systemGpuTemperatureText = "GPU --℃";

    [ObservableProperty]
    private string _systemGpuTemperatureValueText = "--℃";

    [ObservableProperty]
    private string _systemNetworkReceivedText = "--";

    [ObservableProperty]
    private string _systemNetworkSentText = "--";

    [ObservableProperty]
    private string _systemCpuUsageToolTip = string.Empty;

    [ObservableProperty]
    private string _systemCpuTemperatureToolTip = string.Empty;

    [ObservableProperty]
    private string _systemGpuUsageToolTip = string.Empty;

    [ObservableProperty]
    private string _systemGpuTemperatureToolTip = string.Empty;

    public HardwareMonitorViewModel(
        ISystemMetricsMonitor monitor,
        ILocalizationService? localizationService = null)
    {
        _monitor = monitor;
        _localizationService = localizationService;
        _monitor.SnapshotAvailable += Monitor_OnSnapshotAvailable;
        if (_localizationService != null)
        {
            _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;
        }
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
        _lastSnapshot = metrics;
        SystemCpuUsageText = FormatPercent(metrics.CpuUsage);
        SystemCpuTemperatureText = FormatTemperature("CPU", metrics.CpuTemperature);
        SystemCpuTemperatureValueText = FormatTemperatureValue(metrics.CpuTemperature);
        SystemMemoryUsageText = FormatPercent(metrics.MemoryUsage);
        SystemGpuUsageText = FormatPercent(metrics.GpuUsage);
        SystemGpuTemperatureText = FormatTemperature("GPU", metrics.GpuTemperature);
        SystemGpuTemperatureValueText = FormatTemperatureValue(metrics.GpuTemperature);
        SystemNetworkReceivedText = FormatSpeed(metrics.NetworkReceivedBytesPerSecond);
        SystemNetworkSentText = FormatSpeed(metrics.NetworkSentBytesPerSecond);
        SystemCpuUsageToolTip = BuildMetricToolTip(
            metrics.CpuUsage,
            metrics.CpuUsageSource,
            metrics.CpuUsageAvailability,
            metrics.CapturedAtUtc);
        SystemCpuTemperatureToolTip = BuildMetricToolTip(
            metrics.CpuTemperature,
            metrics.CpuTemperatureSource,
            metrics.CpuTemperatureAvailability,
            metrics.CapturedAtUtc);
        SystemGpuUsageToolTip = BuildMetricToolTip(
            metrics.GpuUsage,
            metrics.GpuUsageSource,
            metrics.GpuUsageAvailability,
            metrics.CapturedAtUtc);
        SystemGpuTemperatureToolTip = BuildMetricToolTip(
            metrics.GpuTemperature,
            metrics.GpuTemperatureSource,
            metrics.GpuTemperatureAvailability,
            metrics.CapturedAtUtc);
    }

    private string BuildMetricToolTip(
        double? value,
        string? source,
        HardwareMetricAvailability availability,
        DateTimeOffset capturedAtUtc)
    {
        if (value.HasValue)
        {
            var sourceText = string.IsNullOrWhiteSpace(source)
                ? L("Hardware.SourceUnknown", "Unknown source")
                : source;
            var localTime = capturedAtUtc.ToLocalTime().ToString("g");
            return _localizationService?.Format("Hardware.MetricAvailableFormat", sourceText, localTime)
                ?? $"Source: {sourceText}; updated: {localTime}";
        }

        return availability switch
        {
            HardwareMetricAvailability.NeedsElevation =>
                L("Hardware.NeedsElevation", "Sensor access may require elevation."),
            HardwareMetricAvailability.NoSensor =>
                L("Hardware.NoSensor", "No sensor is available."),
            HardwareMetricAvailability.Stale =>
                L("Hardware.Stale", "The last reading is stale; retry is pending."),
            _ => L("Hardware.ProviderUnavailable", "The metric provider is unavailable.")
        };
    }

    private string L(string key, string fallback) =>
        _localizationService?.GetString(key) ?? fallback;

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_lastSnapshot != null)
        {
            Apply(_lastSnapshot);
        }
    }

    private static string FormatPercent(double? value) => value.HasValue ? $"{value.Value:0}%" : "--";

    private static string FormatTemperature(string label, double? value) =>
        $"{label} {FormatTemperatureValue(value)}";

    private static string FormatTemperatureValue(double? value) =>
        value.HasValue ? $"{value.Value:0}℃" : "--℃";

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
        if (_localizationService != null)
        {
            _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        }
        _monitor.Dispose();
    }
}
