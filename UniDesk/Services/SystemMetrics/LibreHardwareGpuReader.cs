using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using UniDesk.Helpers;

namespace UniDesk.Services;

public sealed class LibreHardwareGpuReader : IDisposable
{
    private Computer? _computer;
    private bool _initialized;
#if DEBUG
    private readonly Dictionary<string, DateTime> _lastSensorLogUtcByHardware = new(StringComparer.OrdinalIgnoreCase);
#endif

    public GpuMetrics Read()
    {
        try
        {
            if (!EnsureInitialized() || _computer == null) return GpuMetrics.Empty;

            var candidates = new List<GpuMetrics>();
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardware(hardware.HardwareType))
                {
                    continue;
                }

                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                }

                var metrics = ReadHardware(hardware);
                if (metrics.HasAnyValue)
                {
                    candidates.Add(metrics);
                }
            }

            return GpuMetricsReader.SelectMetrics(candidates);
        }
        catch
        {
        }

        return GpuMetrics.Empty;
    }

    private bool EnsureInitialized()
    {
        if (_initialized) return _computer != null;
        _initialized = true;

        try
        {
            _computer = new Computer
            {
                IsGpuEnabled = true
            };
            _computer.Open();
            return true;
        }
        catch
        {
            _computer = null;
            return false;
        }
    }

    private GpuMetrics ReadHardware(IHardware hardware)
    {
        var allSensors = GetSensors(hardware).ToList();
        var loadSensors = allSensors
            .Where(sensor => sensor.SensorType == SensorType.Load)
            .Select(sensor => new GpuSensorCandidate(sensor.Name, sensor.Value))
            .ToList();
        var temperatureSensors = allSensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .Select(sensor => new GpuSensorCandidate(sensor.Name, sensor.Value))
            .ToList();
        var usageSelection = SensorSelection.SelectGpuUsageSensor(loadSensors);
        var temperatureSelection = SensorSelection.SelectGpuTemperatureSensor(temperatureSensors);
        LogGpuSensors(hardware, loadSensors, temperatureSensors, usageSelection, temperatureSelection);

        return new GpuMetrics(
            usageSelection?.Value,
            temperatureSelection?.Value,
            hardware.Name,
            GetGpuSourcePriority(hardware.HardwareType),
            hardware.HardwareType is not HardwareType.GpuIntel);
    }

    private static IEnumerable<ISensor> GetSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in subHardware.Sensors)
            {
                yield return sensor;
            }
        }
    }

    private static bool IsGpuHardware(HardwareType type) =>
        type is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel;

    private static int GetGpuSourcePriority(HardwareType type) => type switch
    {
        HardwareType.GpuNvidia => 10,
        HardwareType.GpuAmd => 20,
        HardwareType.GpuIntel => 60,
        _ => 100
    };

    [Conditional("DEBUG")]
    private void LogGpuSensors(
        IHardware hardware,
        IReadOnlyCollection<GpuSensorCandidate> loadSensors,
        IReadOnlyCollection<GpuSensorCandidate> temperatureSensors,
        GpuSensorSelection? usageSelection,
        GpuSensorSelection? temperatureSelection)
    {
#if DEBUG
        var key = $"{hardware.HardwareType}:{hardware.Name}";
        var now = DateTime.UtcNow;
        if (_lastSensorLogUtcByHardware.TryGetValue(key, out var last) &&
            now - last < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastSensorLogUtcByHardware[key] = now;
        var loadSensorText = loadSensors.Count == 0
            ? "未发现 Load 传感器。"
            : string.Join("; ", loadSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var temperatureSensorText = temperatureSensors.Count == 0
            ? "未发现 Temperature 传感器。"
            : string.Join("; ", temperatureSensors.Select(sensor =>
                $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
        var selectedUsageText = usageSelection.HasValue
            ? $"{usageSelection.Value.Name}={usageSelection.Value.Value:0.0}"
            : "未选择可用 GPU 使用率传感器。";
        var selectedTemperatureText = temperatureSelection.HasValue
            ? $"{temperatureSelection.Value.Name}={temperatureSelection.Value.Value:0.0}"
            : "未选择可用 GPU 温度传感器。";
        var message =
            $"GPU 硬件：{hardware.Name}; 类型：{hardware.HardwareType}; Load 传感器：{loadSensorText}; " +
            $"Temperature 传感器：{temperatureSensorText}; 最终 GPU 使用率：{selectedUsageText}; 最终 GPU 温度：{selectedTemperatureText}";

        Debug.WriteLine(message);
        Logger.LogInfo(message, "SystemMetricsService.Gpu");
#endif
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
}
