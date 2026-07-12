namespace UniDesk.Services;

public sealed class LibreHardwareGpuReader : IDisposable
{
    private readonly ILibreHardwareComputerHost _host;
    private readonly bool _ownsHost;
    private readonly bool _refreshBeforeRead;

    public LibreHardwareGpuReader()
        : this(new LibreHardwareComputerHost(), ownsHost: true, refreshBeforeRead: true)
    {
    }

    public LibreHardwareGpuReader(ILibreHardwareComputerHost host)
        : this(host, ownsHost: false, refreshBeforeRead: false)
    {
    }

    private LibreHardwareGpuReader(
        ILibreHardwareComputerHost host,
        bool ownsHost,
        bool refreshBeforeRead)
    {
        _host = host;
        _ownsHost = ownsHost;
        _refreshBeforeRead = refreshBeforeRead;
    }

    public GpuMetrics Read()
    {
        try
        {
            if (_refreshBeforeRead)
            {
                _host.Refresh();
            }

            var candidates = _host.CurrentSensors
                .Where(sensor => IsGpuDevice(sensor.DeviceType))
                .GroupBy(sensor => new { sensor.DeviceId, sensor.DeviceName, sensor.DeviceType })
                .Select(group => ReadHardware(
                    group.Key.DeviceId,
                    group.Key.DeviceName,
                    group.Key.DeviceType,
                    group))
                .Where(metrics => metrics.HasAnyValue)
                .ToList();

            return GpuMetricsReader.SelectMetrics(candidates);
        }
        catch
        {
        }

        return GpuMetrics.Empty;
    }

    private static GpuMetrics ReadHardware(
        string deviceId,
        string deviceName,
        HardwareSensorDeviceType deviceType,
        IEnumerable<HardwareSensorSnapshot> sensors)
    {
        var allSensors = sensors.ToList();
        var loadSensors = allSensors
            .Where(sensor => sensor.SensorType == "Load")
            .Select(sensor => new GpuSensorCandidate(sensor.SensorName, sensor.Value))
            .ToList();
        var temperatureSensors = allSensors
            .Where(sensor => sensor.SensorType == "Temperature")
            .Select(sensor => new GpuSensorCandidate(sensor.SensorName, sensor.Value))
            .ToList();
        var usageSelection = SensorSelection.SelectGpuUsageSensor(loadSensors);
        var temperatureSelection = SensorSelection.SelectGpuTemperatureSensor(temperatureSensors);

        return new GpuMetrics(
            usageSelection?.Value,
            temperatureSelection?.Value,
            deviceName,
            GetGpuSourcePriority(deviceType),
            deviceType is not HardwareSensorDeviceType.GpuIntel,
            usageSource: usageSelection.HasValue ? "LibreHardwareMonitor" : null,
            usageDeviceId: usageSelection.HasValue ? deviceId : null,
            temperatureSource: temperatureSelection.HasValue ? "LibreHardwareMonitor" : null,
            temperatureDeviceId: temperatureSelection.HasValue ? deviceId : null);
    }

    private static bool IsGpuDevice(HardwareSensorDeviceType type) =>
        type is HardwareSensorDeviceType.GpuAmd or
            HardwareSensorDeviceType.GpuNvidia or
            HardwareSensorDeviceType.GpuIntel;

    private static int GetGpuSourcePriority(HardwareSensorDeviceType type) => type switch
    {
        HardwareSensorDeviceType.GpuNvidia => 10,
        HardwareSensorDeviceType.GpuAmd => 20,
        HardwareSensorDeviceType.GpuIntel => 60,
        _ => 100
    };

    public void Dispose()
    {
        if (_ownsHost)
        {
            _host.Dispose();
        }
    }
}
