using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace UniDesk.Services;

public sealed record LibreHardwareHostDiagnosticStatus(
    bool IsInitialized,
    bool IsElevated,
    string? LastError,
    DateTimeOffset? LastRefreshUtc,
    IReadOnlyList<string> HardwareNames);

public interface ILibreHardwareComputerHost : IDisposable
{
    IReadOnlyList<HardwareSensorSnapshot> CurrentSensors { get; }
    LibreHardwareHostDiagnosticStatus DiagnosticStatus { get; }
    void Refresh();
}

public sealed class LibreHardwareComputerHost : ILibreHardwareComputerHost
{
    private readonly object _sync = new();
    private readonly bool _isElevated = GetIsElevated();
    private Computer? _computer;
    private bool _initializationAttempted;
    private bool _disposed;
    private IReadOnlyList<HardwareSensorSnapshot> _currentSensors = Array.Empty<HardwareSensorSnapshot>();
    private LibreHardwareHostDiagnosticStatus _diagnosticStatus;

    public LibreHardwareComputerHost()
    {
        _diagnosticStatus = new(false, _isElevated, null, null, Array.Empty<string>());
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

    public void Refresh()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!EnsureInitialized() || _computer == null)
            {
                _currentSensors = Array.Empty<HardwareSensorSnapshot>();
                return;
            }

            try
            {
                var sensors = new List<HardwareSensorSnapshot>();
                var hardwareNames = new List<string>();
                foreach (var hardware in _computer.Hardware)
                {
                    UpdateHardwareTree(hardware);
                    var deviceId = hardware.Identifier.ToString();
                    var deviceType = MapDeviceType(hardware.HardwareType);
                    hardwareNames.Add($"{deviceType}:{hardware.Name}");
                    CollectSensors(hardware, deviceId, hardware.Name, deviceType, sensors);
                }

                _currentSensors = sensors.ToArray();
                _diagnosticStatus = new(
                    true,
                    _isElevated,
                    null,
                    DateTimeOffset.UtcNow,
                    hardwareNames.ToArray());
            }
            catch (Exception ex)
            {
                _currentSensors = Array.Empty<HardwareSensorSnapshot>();
                _diagnosticStatus = new(
                    true,
                    _isElevated,
                    $"{ex.GetType().Name}: {ex.Message}",
                    DateTimeOffset.UtcNow,
                    _diagnosticStatus.HardwareNames);
            }
        }
    }

    private bool EnsureInitialized()
    {
        if (_initializationAttempted)
        {
            return _computer != null;
        }

        _initializationAttempted = true;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
            _diagnosticStatus = new(true, _isElevated, null, null, Array.Empty<string>());
            return true;
        }
        catch (Exception ex)
        {
            _computer = null;
            _diagnosticStatus = new(
                false,
                _isElevated,
                $"{ex.GetType().Name}: {ex.Message}",
                null,
                Array.Empty<string>());
            return false;
        }
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateHardwareTree(subHardware);
        }
    }

    private static void CollectSensors(
        IHardware hardware,
        string deviceId,
        string deviceName,
        HardwareSensorDeviceType deviceType,
        ICollection<HardwareSensorSnapshot> destination)
    {
        foreach (var sensor in hardware.Sensors)
        {
            destination.Add(new HardwareSensorSnapshot(
                deviceId,
                deviceName,
                deviceType,
                sensor.Name,
                sensor.SensorType.ToString(),
                sensor.Value));
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            CollectSensors(subHardware, deviceId, deviceName, deviceType, destination);
        }
    }

    private static HardwareSensorDeviceType MapDeviceType(HardwareType type) => type switch
    {
        HardwareType.Cpu => HardwareSensorDeviceType.Cpu,
        HardwareType.Motherboard => HardwareSensorDeviceType.Motherboard,
        HardwareType.GpuNvidia => HardwareSensorDeviceType.GpuNvidia,
        HardwareType.GpuAmd => HardwareSensorDeviceType.GpuAmd,
        HardwareType.GpuIntel => HardwareSensorDeviceType.GpuIntel,
        _ => HardwareSensorDeviceType.Other
    };

    private static bool GetIsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _computer?.Close();
            }
            catch
            {
            }

            _computer = null;
            _currentSensors = Array.Empty<HardwareSensorSnapshot>();
        }
    }
}
