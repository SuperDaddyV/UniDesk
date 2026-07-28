using UniDesk.Hardware.Contracts;

namespace UniDesk.Services;

public sealed record HardwareServiceDiagnosticStatus(
    HardwareServiceAvailability Availability,
    PawnIoStatus PawnIo,
    DateTimeOffset? LastSuccessUtc,
    string? LastError,
    string? ServiceVersion = null,
    int ProtocolVersion = 0);

public interface IHardwareServiceDiagnosticsProvider
{
    HardwareServiceDiagnosticStatus ServiceStatus { get; }
}

public sealed class HardwareServiceComputerHost :
    ILibreHardwareComputerHost,
    IHardwareServiceDiagnosticsProvider
{
    private readonly object _sync = new();
    private readonly IHardwareServiceClient _client;
    private IReadOnlyList<HardwareSensorSnapshot> _currentSensors = [];
    private LibreHardwareHostDiagnosticStatus _diagnosticStatus =
        new(false, false, "Hardware service has not been sampled.", null, []);
    private HardwareServiceDiagnosticStatus _serviceStatus = new(
        HardwareServiceAvailability.ServiceUnavailable,
        new PawnIoStatus(false, null),
        null,
        "Hardware service has not been sampled.");
    private bool _disposed;

    public HardwareServiceComputerHost(IHardwareServiceClient client)
    {
        _client = client;
    }

    public IReadOnlyList<HardwareSensorSnapshot> CurrentSensors
    {
        get
        {
            lock (_sync)
            {
                return _currentSensors;
            }
        }
    }

    public LibreHardwareHostDiagnosticStatus DiagnosticStatus
    {
        get
        {
            lock (_sync)
            {
                return _diagnosticStatus;
            }
        }
    }

    public HardwareServiceDiagnosticStatus ServiceStatus
    {
        get
        {
            lock (_sync)
            {
                return _serviceStatus;
            }
        }
    }

    public void Refresh()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var result = _client.GetSnapshot();
            var response = result.Response;
            if (response == null || response.Availability != HardwareServiceAvailability.Available)
            {
                _currentSensors = [];
                _diagnosticStatus = new(false, false, result.Error, null, []);
                _serviceStatus = new(
                    result.Availability,
                    response?.PawnIo ?? new PawnIoStatus(false, null),
                    _serviceStatus.LastSuccessUtc,
                    result.Error,
                    response?.ServiceVersion,
                    response?.ProtocolVersion ?? 0);
                return;
            }

            _currentSensors = response.Sensors.Select(MapSensor).ToArray();
            _diagnosticStatus = new(
                response.Provider.IsInitialized,
                response.Provider.IsElevated,
                response.Provider.LastError,
                response.Provider.LastRefreshUtc,
                response.Provider.HardwareNames.ToArray());
            _serviceStatus = new(
                response.Availability,
                response.PawnIo,
                DateTimeOffset.UtcNow,
                response.Error,
                response.ServiceVersion,
                response.ProtocolVersion);
        }
    }

    private static HardwareSensorSnapshot MapSensor(HardwareSensorDto sensor) =>
        new(
            sensor.DeviceId,
            sensor.DeviceName,
            sensor.DeviceType switch
            {
                HardwareDeviceType.Cpu => HardwareSensorDeviceType.Cpu,
                HardwareDeviceType.Motherboard => HardwareSensorDeviceType.Motherboard,
                HardwareDeviceType.GpuNvidia => HardwareSensorDeviceType.GpuNvidia,
                HardwareDeviceType.GpuAmd => HardwareSensorDeviceType.GpuAmd,
                HardwareDeviceType.GpuIntel => HardwareSensorDeviceType.GpuIntel,
                _ => HardwareSensorDeviceType.Other
            },
            sensor.SensorName,
            sensor.SensorType,
            sensor.Value);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.Dispose();
            _currentSensors = [];
        }
    }
}
