using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using UniDesk.Hardware.Contracts;

namespace UniDesk.HardwareService;

public sealed class LibreHardwareSnapshotCollector : IDisposable
{
    private readonly bool _isElevated = GetIsElevated();
    private readonly TimeProvider _timeProvider;
    private readonly InitializationRetryPolicy _initializationRetryPolicy;
    private Computer? _computer;
    private string? _lastInitializationError;
    private bool _disposed;

    public LibreHardwareSnapshotCollector()
        : this(TimeProvider.System)
    {
    }

    public LibreHardwareSnapshotCollector(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _initializationRetryPolicy = new InitializationRetryPolicy(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(1));
    }

    public HardwareServiceSnapshotResponse Collect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pawnIo = new PawnIoStatus(PawnIo.IsInstalled, PawnIo.Version?.ToString());
        if (!pawnIo.IsInstalled)
        {
            const string error = "PawnIO is not installed.";
            return CreateUnavailable(HardwareServiceAvailability.DriverUnavailable, pawnIo, error);
        }

        if (!EnsureInitialized(out var initializationError) || _computer == null)
        {
            return CreateUnavailable(
                HardwareServiceAvailability.Error,
                pawnIo,
                initializationError ?? "LibreHardwareMonitor failed to initialize.");
        }

        try
        {
            var sensors = new List<HardwareSensorDto>();
            var hardwareNames = new List<string>();
            foreach (var hardware in _computer.Hardware)
            {
                var deviceId = hardware.Identifier.ToString();
                var deviceType = MapDeviceType(hardware.HardwareType);
                hardwareNames.Add(GetHardwareDiagnosticName(
                    hardware.HardwareType,
                    deviceType,
                    hardware.Name));
                if (!ShouldUpdateHardware(hardware.HardwareType))
                {
                    continue;
                }

                UpdateHardwareTree(hardware);
                CollectSensors(hardware, deviceId, hardware.Name, deviceType, sensors);
            }

            var capturedAt = DateTimeOffset.UtcNow;
            return new HardwareServiceSnapshotResponse(
                HardwareIpcProtocol.CurrentVersion,
                HardwareServiceAvailability.Available,
                null,
                capturedAt,
                pawnIo,
                new HardwareProviderStatus(true, _isElevated, null, capturedAt, hardwareNames),
                sensors,
                GetServiceVersion());
        }
        catch (Exception ex)
        {
            return CreateUnavailable(
                HardwareServiceAvailability.Error,
                pawnIo,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool EnsureInitialized(out string? error)
    {
        error = null;
        if (_computer != null)
        {
            return true;
        }

        var nowUtc = _timeProvider.GetUtcNow();
        if (!_initializationRetryPolicy.CanAttempt(nowUtc))
        {
            error = _lastInitializationError ?? "LibreHardwareMonitor initialization is waiting to retry.";
            return false;
        }

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
            _lastInitializationError = null;
            _initializationRetryPolicy.RecordSuccess();
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                _computer?.Close();
            }
            catch
            {
            }

            _computer = null;
            _lastInitializationError = $"{ex.GetType().Name}: {ex.Message}";
            _initializationRetryPolicy.RecordFailure(nowUtc);
            error = _lastInitializationError;
            return false;
        }
    }

    private HardwareServiceSnapshotResponse CreateUnavailable(
        HardwareServiceAvailability availability,
        PawnIoStatus pawnIo,
        string error) =>
        new(
            HardwareIpcProtocol.CurrentVersion,
            availability,
            error,
            DateTimeOffset.UtcNow,
            pawnIo,
            new HardwareProviderStatus(false, _isElevated, error, null, []),
            [],
            GetServiceVersion());

    private static string GetServiceVersion() =>
        typeof(LibreHardwareSnapshotCollector).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    internal static void UpdateHardwareTree(IHardware hardware)
    {
        if (!ShouldUpdateHardware(hardware.HardwareType))
        {
            return;
        }

        hardware.Update();
        foreach (var child in hardware.SubHardware)
        {
            UpdateHardwareTree(child);
        }
    }

    internal static bool ShouldUpdateHardware(HardwareType hardwareType) =>
        hardwareType is not HardwareType.GpuAmd;

    internal static string GetHardwareDiagnosticName(
        HardwareType hardwareType,
        HardwareDeviceType deviceType,
        string hardwareName) =>
        ShouldUpdateHardware(hardwareType)
            ? $"{deviceType}:{hardwareName}"
            : $"{deviceType}:{hardwareName} [LibreHardwareMonitor AMD update isolated]";

    private static void CollectSensors(
        IHardware hardware,
        string deviceId,
        string deviceName,
        HardwareDeviceType deviceType,
        ICollection<HardwareSensorDto> destination)
    {
        if (!ShouldUpdateHardware(hardware.HardwareType))
        {
            return;
        }

        foreach (var sensor in hardware.Sensors)
        {
            destination.Add(new HardwareSensorDto(
                deviceId,
                deviceName,
                deviceType,
                sensor.Name,
                sensor.SensorType.ToString(),
                sensor.Value));
        }

        foreach (var child in hardware.SubHardware)
        {
            CollectSensors(child, deviceId, deviceName, deviceType, destination);
        }
    }

    private static HardwareDeviceType MapDeviceType(HardwareType type) => type switch
    {
        HardwareType.Cpu => HardwareDeviceType.Cpu,
        HardwareType.Motherboard => HardwareDeviceType.Motherboard,
        HardwareType.GpuNvidia => HardwareDeviceType.GpuNvidia,
        HardwareType.GpuAmd => HardwareDeviceType.GpuAmd,
        HardwareType.GpuIntel => HardwareDeviceType.GpuIntel,
        _ => HardwareDeviceType.Other
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
    }
}
