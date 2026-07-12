using System.Reflection;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class SensorDiagnosticReporter : ISensorDiagnosticsService
{
    private static readonly Regex WindowsPathPattern = new(
        "(?i)\\b[A-Z]:\\\\[^\\r\\n|;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Ipv4Pattern = new(
        "\\b(?:[0-9]{1,3}\\.){3}[0-9]{1,3}\\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MacPattern = new(
        "(?i)\\b(?:[0-9A-F]{2}[:-]){5}[0-9A-F]{2}\\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHardwareMetricsDiagnosticsSource _source;
    private readonly string _outputDirectory;

    public SensorDiagnosticReporter(IHardwareMetricsDiagnosticsSource source)
        : this(source, DirectoryHelper.LogsDirectory)
    {
    }

    public SensorDiagnosticReporter(
        IHardwareMetricsDiagnosticsSource source,
        string outputDirectory)
    {
        _source = source;
        _outputDirectory = outputDirectory;
    }

    public async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outputDirectory);
        var snapshot = _source.CaptureDiagnostics();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
        var report = BuildReport(snapshot, version, Environment.OSVersion.VersionString);
        var path = Path.Combine(
            _outputDirectory,
            $"hardware-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        await File.WriteAllTextAsync(path, report, new UTF8Encoding(false), cancellationToken);
        return path;
    }

    public static string BuildReport(
        HardwareMetricsDiagnosticsSnapshot diagnostics,
        string applicationVersion,
        string windowsVersion)
    {
        var builder = new StringBuilder();
        builder.AppendLine("UniDesk Hardware Diagnostics");
        builder.AppendLine("Schema: 1");
        builder.AppendLine($"Application version: {Sanitize(applicationVersion)}");
        builder.AppendLine($"Windows version: {Sanitize(windowsVersion)}");
        builder.AppendLine($"Captured UTC: {diagnostics.CapturedAtUtc:O}");
        builder.AppendLine($"Elevated: {diagnostics.LibreHardwareStatus.IsElevated}");
        builder.AppendLine();

        builder.AppendLine("[Providers]");
        builder.AppendLine(
            $"LibreHardwareMonitor | initialized={diagnostics.LibreHardwareStatus.IsInitialized} | " +
            $"lastRefreshUtc={FormatTimestamp(diagnostics.LibreHardwareStatus.LastRefreshUtc)} | " +
            $"error={Sanitize(diagnostics.LibreHardwareStatus.LastError)}");
        if (diagnostics.GpuEngineStatus != null)
        {
            var gpu = diagnostics.GpuEngineStatus;
            builder.AppendLine(
                $"Windows GPU Engine | canAttempt={gpu.CanAttempt} | failures={gpu.ConsecutiveFailures} | " +
                $"nextRetryUtc={FormatTimestamp(gpu.NextRetryAtUtc)} | activeCounters={gpu.ActiveCounterCount} | " +
                $"lastSampleUtc={FormatTimestamp(gpu.LastSampleUtc)} | error={Sanitize(gpu.LastFailureReason)}");
        }
        else
        {
            builder.AppendLine("Windows GPU Engine | unavailable");
        }

        foreach (var hardware in diagnostics.LibreHardwareStatus.HardwareNames)
        {
            builder.AppendLine($"Hardware | {Sanitize(hardware)}");
        }

        builder.AppendLine();
        builder.AppendLine("[Sensors]");
        foreach (var sensor in diagnostics.Sensors)
        {
            builder.AppendLine(
                $"{sensor.DeviceType} | device={Sanitize(sensor.DeviceName)} | id={Sanitize(sensor.DeviceId)} | " +
                $"type={Sanitize(sensor.SensorType)} | sensor={Sanitize(sensor.SensorName)} | " +
                $"value={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.###") : "null")}");
        }

        builder.AppendLine();
        builder.AppendLine("[Recent snapshots]");
        foreach (var snapshot in diagnostics.RecentSnapshots.TakeLast(3))
        {
            AppendSnapshot(builder, snapshot);
        }

        return builder.ToString();
    }

    private static void AppendSnapshot(StringBuilder builder, SystemMetricsSnapshot snapshot)
    {
        builder.AppendLine(
            $"{snapshot.CapturedAtUtc:O} | " +
            $"cpuUsage={FormatMetric(snapshot.CpuUsage, snapshot.CpuUsageSource, snapshot.CpuUsageDeviceId, snapshot.CpuUsageAvailability, snapshot.CpuUsageReason)} | " +
            $"cpuTemperature={FormatMetric(snapshot.CpuTemperature, snapshot.CpuTemperatureSource, snapshot.CpuTemperatureDeviceId, snapshot.CpuTemperatureAvailability, snapshot.CpuTemperatureReason)} | " +
            $"gpuUsage={FormatMetric(snapshot.GpuUsage, snapshot.GpuUsageSource, snapshot.GpuUsageDeviceId, snapshot.GpuUsageAvailability, snapshot.GpuUsageReason)} | " +
            $"gpuTemperature={FormatMetric(snapshot.GpuTemperature, snapshot.GpuTemperatureSource, snapshot.GpuTemperatureDeviceId, snapshot.GpuTemperatureAvailability, snapshot.GpuTemperatureReason)}");
    }

    private static string FormatMetric(
        double? value,
        string? source,
        string? deviceId,
        HardwareMetricAvailability availability,
        string? reason) =>
        $"value={(value.HasValue ? value.Value.ToString("0.###") : "null")}," +
        $"source={Sanitize(source)},device={Sanitize(deviceId)},availability={availability},reason={Sanitize(reason)}";

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value.HasValue ? value.Value.ToString("O") : "none";

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var sanitized = value;
        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            sanitized = sanitized.Replace(userName, "[redacted]", StringComparison.OrdinalIgnoreCase);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            sanitized = sanitized.Replace(userProfile, "[path]", StringComparison.OrdinalIgnoreCase);
        }

        sanitized = WindowsPathPattern.Replace(sanitized, "[path]");
        sanitized = Ipv4Pattern.Replace(sanitized, "[network]");
        sanitized = MacPattern.Replace(sanitized, "[network]");
        return sanitized.Replace('\r', ' ').Replace('\n', ' ');
    }
}
