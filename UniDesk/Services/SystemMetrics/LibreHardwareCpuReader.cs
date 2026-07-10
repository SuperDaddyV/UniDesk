using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using UniDesk.Helpers;

namespace UniDesk.Services;

public sealed class LibreHardwareCpuReader : IDisposable
{
    private Computer? _computer;
    private bool _initialized;
    private DateTime _lastSensorLogUtc = DateTime.MinValue;
    private DateTime _lastReaderErrorLogUtc = DateTime.MinValue;
    private DateTime _lastWindowsThermalZoneErrorLogUtc = DateTime.MinValue;

    public CpuMetrics Read()
    {
        try
        {
            if (!EnsureInitialized() || _computer == null)
            {
                return CpuMetrics.Empty;
            }

            var cpuHardwareNames = new List<string>();
            var loadSensors = new List<CpuUsageSensorCandidate>();
            var temperatureSensors = new List<CpuTemperatureSensorCandidate>();
            var motherboardTemperatureSensors = new List<CpuTemperatureSensorCandidate>();
            var windowsThermalZoneSensors = new List<CpuTemperatureSensorCandidate>();

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu)
                {
                    continue;
                }

                cpuHardwareNames.Add(hardware.Name);
                UpdateHardwareTree(hardware);

                var allSensors = GetSensors(hardware).ToList();
                loadSensors.AddRange(allSensors
                    .Where(sensor => sensor.SensorType == SensorType.Load)
                    .Select(sensor => new CpuUsageSensorCandidate(sensor.Name, sensor.Value)));
                temperatureSensors.AddRange(allSensors
                    .Where(sensor => sensor.SensorType == SensorType.Temperature)
                    .Select(sensor => new CpuTemperatureSensorCandidate(sensor.Name, sensor.Value)));
            }

            var cpuHardwareName = cpuHardwareNames.Count == 0
                ? null
                : string.Join("; ", cpuHardwareNames);
            var loadSelection = SensorSelection.SelectCpuUsageSensor(loadSensors);
            var cpuTemperatureSelection = SensorSelection.SelectCpuTemperatureSensor(temperatureSensors, cpuHardwareName);
            CpuTemperatureSensorSelection? motherboardTemperatureSelection = null;
            CpuTemperatureSensorSelection? windowsThermalZoneSelection = null;

            if (!cpuTemperatureSelection.HasValue)
            {
                motherboardTemperatureSensors = ReadMotherboardTemperatureSensors().ToList();
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
                return new CpuMetrics(loadSelection?.Value, temperatureSelection?.Value);
            }
        }
        catch (Exception ex)
        {
            LogCpuTemperatureReaderError(ex);
        }

        return CpuMetrics.Empty;
    }

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return _computer != null;
        }

        _initialized = true;

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true
            };
            _computer.Open();
            return true;
        }
        catch (Exception ex)
        {
            _computer = null;
            LogCpuTemperatureReaderError(ex);
            return false;
        }
    }

    private IEnumerable<CpuTemperatureSensorCandidate> ReadMotherboardTemperatureSensors()
    {
        if (_computer == null)
        {
            yield break;
        }

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Motherboard)
            {
                continue;
            }

            UpdateHardwareTree(hardware);
            foreach (var sensor in GetSensors(hardware).Where(sensor => sensor.SensorType == SensorType.Temperature))
            {
                yield return new CpuTemperatureSensorCandidate(sensor.Name, sensor.Value);
            }
        }
    }

    private List<CpuTemperatureSensorCandidate> ReadWindowsThermalZoneTemperatureSensors()
    {
        var sensors = new List<CpuTemperatureSensorCandidate>();

        try
        {
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
        }
        catch (Exception ex)
        {
            LogWindowsThermalZoneReaderError(ex);
        }

        return sensors;
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateHardwareTree(subHardware);
        }
    }

    private static IEnumerable<ISensor> GetSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in GetSensors(subHardware))
            {
                yield return sensor;
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _computer?.Close();
        }
        catch
        {
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
            $"CPU 温度读取失败：{ex.GetType().Name}: {ex.Message}。部分硬件传感器可能需要管理员权限或主板驱动支持。",
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
            $"Windows ACPI Thermal Zone 读取失败：{ex.GetType().Name}: {ex.Message}。将继续使用其它 CPU 温度来源。",
            "SystemMetricsService.WindowsThermalZone");
    }
}
