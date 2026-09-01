using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using UniDesk.Hardware.Contracts;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class LibreHardwareCpuReader : IDisposable
{
    private readonly ILibreHardwareComputerHost _host;
    private readonly bool _ownsHost;
    private readonly bool _refreshBeforeRead;
    private readonly Func<IReadOnlyList<CpuTemperatureSensorCandidate>> _readWindowsThermalZones;
    private readonly ReaderFailureBackoff _windowsThermalZoneBackoff = new(TimeSpan.FromMinutes(5));
    private DateTime _lastSensorLogUtc = DateTime.MinValue;
    private DateTime _lastReaderErrorLogUtc = DateTime.MinValue;
    private DateTime _lastWindowsThermalZoneErrorLogUtc = DateTime.MinValue;

    public LibreHardwareCpuReader()
        : this(new LibreHardwareComputerHost(), ownsHost: true, refreshBeforeRead: true)
    {
    }

    public LibreHardwareCpuReader(ILibreHardwareComputerHost host)
        : this(host, ownsHost: false, refreshBeforeRead: false, readWindowsThermalZones: null)
    {
    }

    public LibreHardwareCpuReader(
        ILibreHardwareComputerHost host,
        Func<IReadOnlyList<CpuTemperatureSensorCandidate>> readWindowsThermalZones)
        : this(host, ownsHost: false, refreshBeforeRead: false, readWindowsThermalZones)
    {
    }

    private LibreHardwareCpuReader(
        ILibreHardwareComputerHost host,
        bool ownsHost,
        bool refreshBeforeRead,
        Func<IReadOnlyList<CpuTemperatureSensorCandidate>>? readWindowsThermalZones = null)
    {
        _host = host;
        _ownsHost = ownsHost;
        _refreshBeforeRead = refreshBeforeRead;
        _readWindowsThermalZones = readWindowsThermalZones ?? QueryWindowsThermalZoneTemperatureSensors;
    }

    public CpuMetrics Read()
    {
        try
        {
            if (_refreshBeforeRead)
            {
                _host.Refresh();
            }

            var snapshot = _host.CurrentSensors;
            var cpuSensors = snapshot
                .Where(sensor => sensor.DeviceType == HardwareSensorDeviceType.Cpu)
                .ToList();
            var cpuHardwareNames = cpuSensors
                .Select(sensor => sensor.DeviceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var cpuDeviceId = cpuSensors.Select(sensor => sensor.DeviceId).FirstOrDefault();
            var loadSensors = cpuSensors
                .Where(sensor => sensor.SensorType == "Load")
                .Select(sensor => new CpuUsageSensorCandidate(sensor.SensorName, sensor.Value))
                .ToList();
            var temperatureSensors = cpuSensors
                .Where(sensor => sensor.SensorType == "Temperature")
                .Select(sensor => new CpuTemperatureSensorCandidate(sensor.SensorName, sensor.Value))
                .ToList();
            var motherboardTemperatureSensors = new List<CpuTemperatureSensorCandidate>();
            var windowsThermalZoneSensors = new List<CpuTemperatureSensorCandidate>();

            var cpuHardwareName = cpuHardwareNames.Count == 0
                ? null
                : string.Join("; ", cpuHardwareNames);
            var loadSelection = SensorSelection.SelectCpuUsageSensor(loadSensors);
            var cpuTemperatureSelection = SensorSelection.SelectCpuTemperatureSensor(temperatureSensors, cpuHardwareName);
            CpuTemperatureSensorSelection? motherboardTemperatureSelection = null;
            CpuTemperatureSensorSelection? windowsThermalZoneSelection = null;

            if (!cpuTemperatureSelection.HasValue)
            {
                motherboardTemperatureSensors = ReadMotherboardTemperatureSensors(snapshot).ToList();
                motherboardTemperatureSelection = SensorSelection.SelectCpuMotherboardTemperatureSensor(
                    motherboardTemperatureSensors,
                    cpuHardwareName);
            }

            if (!cpuTemperatureSelection.HasValue && !motherboardTemperatureSelection.HasValue)
            {
                windowsThermalZoneSensors = ReadWindowsThermalZoneTemperatureSensors();
                windowsThermalZoneSelection = SensorSelection.SelectWindowsThermalZoneTemperatureSensor(windowsThermalZoneSensors);
            }

            var temperatureSelection = SensorSelection.SelectCpuTemperatureProvider(
                cpuTemperatureSelection,
                motherboardTemperatureSelection,
                windowsThermalZoneSelection);

            LogCpuSensors(
                cpuHardwareName,
                loadSensors,
                temperatureSensors,
                motherboardTemperatureSensors,
                windowsThermalZoneSensors,
                loadSelection,
                cpuTemperatureSelection,
                temperatureSelection,
                motherboardTemperatureSelection,
                windowsThermalZoneSelection);

            if (loadSelection.HasValue || temperatureSelection.HasValue)
            {
                var motherboardDeviceId = snapshot
                    .Where(sensor => sensor.DeviceType == HardwareSensorDeviceType.Motherboard)
                    .Select(sensor => sensor.DeviceId)
                    .FirstOrDefault();
                var temperatureDeviceId = cpuTemperatureSelection.HasValue
                    ? cpuDeviceId
                    : motherboardTemperatureSelection.HasValue
                        ? motherboardDeviceId
                        : windowsThermalZoneSelection.HasValue
                            ? "windows-acpi"
                            : null;
                return new CpuMetrics(
                    loadSelection?.Value,
                    temperatureSelection?.Value,
                    usageSource: loadSelection.HasValue ? "LibreHardwareMonitor" : null,
                    usageDeviceId: loadSelection.HasValue ? cpuDeviceId : null,
                    temperatureSource: temperatureSelection?.Source,
                    temperatureDeviceId: temperatureDeviceId);
            }

            var serviceStatus = (_host as IHardwareServiceDiagnosticsProvider)?.ServiceStatus;
            if (serviceStatus is { Availability: not HardwareServiceAvailability.Available })
            {
                var serviceReason = serviceStatus.LastError ?? serviceStatus.Availability switch
                {
                    HardwareServiceAvailability.DriverUnavailable => "PawnIO driver is unavailable.",
                    HardwareServiceAvailability.ServiceNotInstalled => "Hardware service is not installed.",
                    HardwareServiceAvailability.ServiceStopped => "Hardware service is stopped.",
                    HardwareServiceAvailability.ProtocolMismatch => "Hardware service protocol mismatch.",
                    HardwareServiceAvailability.TimedOut => "Hardware service timed out.",
                    _ => "Hardware service is unavailable."
                };
                return new CpuMetrics(
                    null,
                    null,
                    usageAvailability: HardwareMetricAvailability.ProviderUnavailable,
                    temperatureAvailability: HardwareMetricAvailability.ProviderUnavailable,
                    usageReason: serviceReason,
                    temperatureReason: serviceReason);
            }

            var needsElevation = !_host.DiagnosticStatus.IsElevated;
            return new CpuMetrics(
                null,
                null,
                temperatureAvailability: needsElevation
                    ? HardwareMetricAvailability.NeedsElevation
                    : HardwareMetricAvailability.NoSensor,
                usageReason: _host.DiagnosticStatus.LastError,
                temperatureReason: _host.DiagnosticStatus.LastError ??
                    (needsElevation ? "Sensor access may require elevation." : "No CPU temperature sensor was found."));
        }
        catch (Exception ex)
        {
            LogCpuTemperatureReaderError(ex);
        }

        return CpuMetrics.Empty;
    }

    private static IEnumerable<CpuTemperatureSensorCandidate> ReadMotherboardTemperatureSensors(
        IReadOnlyList<HardwareSensorSnapshot> snapshot)
    {
        foreach (var sensor in snapshot)
        {
            if (sensor.DeviceType != HardwareSensorDeviceType.Motherboard ||
                sensor.SensorType != "Temperature")
            {
                continue;
            }

            yield return new CpuTemperatureSensorCandidate(sensor.SensorName, sensor.Value);
        }
    }

    private List<CpuTemperatureSensorCandidate> ReadWindowsThermalZoneTemperatureSensors()
    {
        if (!_windowsThermalZoneBackoff.CanAttempt)
        {
            return [];
        }

        try
        {
            var sensors = _readWindowsThermalZones().ToList();
            _windowsThermalZoneBackoff.RecordSuccess();
            return sensors;
        }
        catch (Exception ex)
        {
            _windowsThermalZoneBackoff.RecordFailure($"{ex.GetType().Name}: {ex.Message}");
            LogWindowsThermalZoneReaderError(ex);
            return [];
        }
    }

    private static IReadOnlyList<CpuTemperatureSensorCandidate> QueryWindowsThermalZoneTemperatureSensors()
    {
        var sensors = new List<CpuTemperatureSensorCandidate>();
        using var searcher = new ManagementObjectSearcher(
            "root\\WMI",
            "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

        foreach (ManagementObject item in searcher.Get())
        {
            var name = item["InstanceName"]?.ToString();
            var rawValue = item["CurrentTemperature"];
            if (rawValue == null)
            {
                continue;
            }

            var celsius = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture) / 10d - 273.15d;
            sensors.Add(new CpuTemperatureSensorCandidate(
                string.IsNullOrWhiteSpace(name) ? "ACPI Thermal Zone" : $"ACPI {name}",
                celsius));
        }

        return sensors;
    }

    public void Dispose()
    {
        if (_ownsHost)
        {
            _host.Dispose();
        }
    }

    private void LogCpuSensors(
        string? hardwareName,
        IReadOnlyCollection<CpuUsageSensorCandidate> loadSensors,
        IReadOnlyCollection<CpuTemperatureSensorCandidate> temperatureSensors,
        IReadOnlyCollection<CpuTemperatureSensorCandidate> motherboardTemperatureSensors,
        IReadOnlyCollection<CpuTemperatureSensorCandidate> windowsThermalZoneSensors,
        CpuUsageSensorSelection? loadSelection,
        CpuTemperatureSensorSelection? cpuTemperatureSelection,
        CpuTemperatureProviderSelection? temperatureSelection,
        CpuTemperatureSensorSelection? motherboardTemperatureSelection,
        CpuTemperatureSensorSelection? windowsThermalZoneSelection)
    {
        var now = DateTime.UtcNow;
        if (now - _lastSensorLogUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastSensorLogUtc = now;

        var loadSensorText = loadSensors.Count == 0
            ? "未发现 Load 传感器。"
            : string.Join("; ", loadSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var temperatureSensorText = temperatureSensors.Count == 0
            ? "未发现 Temperature 传感器。部分硬件传感器可能需要管理员权限或主板驱动支持。"
            : string.Join("; ", temperatureSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var motherboardTemperatureSensorText = motherboardTemperatureSensors.Count == 0
            ? "未发现主板 Temperature 传感器。"
            : string.Join("; ", motherboardTemperatureSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var windowsThermalZoneSensorText = windowsThermalZoneSensors.Count == 0
            ? "未发现 Windows ACPI Thermal Zone。"
            : string.Join("; ", windowsThermalZoneSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var selectedLoadText = loadSelection.HasValue
            ? $"{loadSelection.Value.Name}={loadSelection.Value.Value:0.0}"
            : "未选择可用 CPU 使用率传感器。";
        var selectedCpuTemperatureText = cpuTemperatureSelection.HasValue
            ? $"{cpuTemperatureSelection.Value.Name}={cpuTemperatureSelection.Value.Value:0.0}"
            : "未选择 LibreHardwareMonitor CPU 温度传感器。";
        var selectedTemperatureText = temperatureSelection.HasValue
            ? $"{temperatureSelection.Value.Source}/{temperatureSelection.Value.Name}={temperatureSelection.Value.Value:0.0}"
            : "未选择可用 CPU 温度传感器。";
        var selectedMotherboardTemperatureText = motherboardTemperatureSelection.HasValue
            ? $"{motherboardTemperatureSelection.Value.Name}={motherboardTemperatureSelection.Value.Value:0.0}"
            : "未选择主板 CPU 温度传感器。";
        var selectedWindowsThermalZoneText = windowsThermalZoneSelection.HasValue
            ? $"{windowsThermalZoneSelection.Value.Name}={windowsThermalZoneSelection.Value.Value:0.0}"
            : "未选择 Windows ACPI Thermal Zone。";
        var message =
            $"CPU 硬件：{(string.IsNullOrWhiteSpace(hardwareName) ? "未发现 CPU 硬件" : hardwareName)}; " +
            $"进程权限：{GetProcessPrivilegeText()}; 架构：{RuntimeInformation.ProcessArchitecture}; " +
            $"Load 传感器：{loadSensorText}; CPU Temperature 传感器：{temperatureSensorText}; " +
            $"主板 Temperature 传感器：{motherboardTemperatureSensorText}; Windows ACPI Thermal Zone：{windowsThermalZoneSensorText}; " +
            $"最终 CPU 使用率：{selectedLoadText}; LHM CPU 温度：{selectedCpuTemperatureText}; " +
            $"主板兜底温度：{selectedMotherboardTemperatureText}; Windows 兜底温度：{selectedWindowsThermalZoneText}; " +
            $"最终 CPU 温度：{selectedTemperatureText}";

        Debug.WriteLine(message);
        Logger.LogInfo(message, "SystemMetricsService.Cpu");
    }

    private static string GetProcessPrivilegeText()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator) ? "管理员" : "普通用户";
        }
        catch
        {
            return "未知";
        }
    }

    private void LogCpuTemperatureReaderError(Exception ex)
    {
        var now = DateTime.UtcNow;
        if (now - _lastReaderErrorLogUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastReaderErrorLogUtc = now;
        Debug.WriteLine($"CPU 温度读取失败：{ex.GetType().Name}: {ex.Message}");
        Logger.LogWarning(
            $"CPU 温度读取失败：{ex.GetType().Name}。部分硬件传感器可能需要管理员权限或主板驱动支持。",
            "SystemMetricsService.CpuTemperature");
    }

    private void LogWindowsThermalZoneReaderError(Exception ex)
    {
        var now = DateTime.UtcNow;
        if (now - _lastWindowsThermalZoneErrorLogUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastWindowsThermalZoneErrorLogUtc = now;
        Logger.LogWarning(
            $"Windows ACPI Thermal Zone 读取失败：{ex.GetType().Name}。将继续使用其它 CPU 温度来源。",
            "SystemMetricsService.WindowsThermalZone");
    }
}
