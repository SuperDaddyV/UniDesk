using UniDesk.Models;

namespace UniDesk.Services;

public sealed class SystemMetricsService :
    ISystemMetricsService,
    IHardwareMetricsDiagnosticsSource,
    IDisposable
{
    private readonly ICpuMetricsReader _cpuReader;
    private readonly IGpuMetricsReader _gpuReader;
    private readonly IMemoryMetricsReader _memoryReader;
    private readonly INetworkMetricsReader _networkReader;
    private readonly ILibreHardwareComputerHost? _libreHost;
    private readonly bool _ownsLibreHost;
    private readonly TemperatureSpikeFilter _cpuTemperatureFilter = new();
    private readonly TemperatureSpikeFilter _gpuTemperatureFilter = new();
    private readonly object _historySync = new();
    private readonly Queue<SystemMetricsSnapshot> _recentSnapshots = new();
    private bool _disposed;

    public SystemMetricsService()
    {
        var libreHost = new LibreHardwareComputerHost();
        _libreHost = libreHost;
        _ownsLibreHost = true;
        _cpuReader = new CpuMetricsReader(libreHost);
        _gpuReader = new GpuMetricsReader(libreHost);
        _memoryReader = new WindowsMemoryMetricsReader();
        _networkReader = new NetworkMetricsReader();
    }

    public SystemMetricsService(
        ICpuMetricsReader cpuReader,
        IGpuMetricsReader gpuReader,
        IMemoryMetricsReader memoryReader,
        INetworkMetricsReader networkReader,
        ILibreHardwareComputerHost? libreHost = null,
        bool ownsLibreHost = false)
    {
        _cpuReader = cpuReader;
        _gpuReader = gpuReader;
        _memoryReader = memoryReader;
        _networkReader = networkReader;
        _libreHost = libreHost;
        _ownsLibreHost = ownsLibreHost;
    }

    public SystemMetricsSnapshot Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _libreHost?.Refresh();
        var cpu = _cpuReader.Read();
        var gpu = _gpuReader.Read();
        var memory = _memoryReader.Read();
        var network = _networkReader.Read();
        var cpuTemperature = _cpuTemperatureFilter.Apply(cpu.CpuTemperature, cpu.TemperatureSource);
        var gpuTemperature = _gpuTemperatureFilter.Apply(gpu.GpuTemperature, gpu.TemperatureSource);
        var snapshot = new SystemMetricsSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            CpuUsage = cpu.CpuUsage,
            CpuTemperature = cpuTemperature,
            MemoryUsage = memory.UsagePercent,
            GpuUsage = gpu.GpuUsage,
            GpuTemperature = gpuTemperature,
            NetworkReceivedBytesPerSecond = network.ReceivedBytesPerSecond,
            NetworkSentBytesPerSecond = network.SentBytesPerSecond,
            CpuUsageSource = cpu.UsageSource,
            CpuUsageDeviceId = cpu.UsageDeviceId,
            CpuTemperatureSource = cpu.TemperatureSource,
            CpuTemperatureDeviceId = cpu.TemperatureDeviceId,
            GpuUsageSource = gpu.UsageSource,
            GpuUsageDeviceId = gpu.UsageDeviceId,
            GpuTemperatureSource = gpu.TemperatureSource,
            GpuTemperatureDeviceId = gpu.TemperatureDeviceId,
            CpuUsageAvailability = cpu.UsageAvailability,
            CpuTemperatureAvailability = cpu.TemperatureAvailability,
            GpuUsageAvailability = gpu.UsageAvailability,
            GpuTemperatureAvailability = gpu.TemperatureAvailability,
            CpuUsageReason = cpu.UsageReason,
            CpuTemperatureReason = cpu.TemperatureReason,
            GpuUsageReason = gpu.UsageReason,
            GpuTemperatureReason = gpu.TemperatureReason
        };
        lock (_historySync)
        {
            _recentSnapshots.Enqueue(snapshot);
            while (_recentSnapshots.Count > 3)
            {
                _recentSnapshots.Dequeue();
            }
        }

        return snapshot;
    }

    public HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SystemMetricsSnapshot[] history;
        lock (_historySync)
        {
            history = _recentSnapshots.ToArray();
        }

        var libreStatus = _libreHost?.DiagnosticStatus ??
            new LibreHardwareHostDiagnosticStatus(false, false, "not configured", null, []);
        var sensors = _libreHost?.CurrentSensors.ToArray() ?? [];
        var gpuEngineStatus = (_gpuReader as GpuMetricsReader)?.GpuEngineDiagnosticStatus;
        return new HardwareMetricsDiagnosticsSnapshot(
            DateTimeOffset.UtcNow,
            libreStatus,
            gpuEngineStatus,
            sensors,
            history);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cpuReader.Dispose();
        _gpuReader.Dispose();
        _networkReader.Dispose();
        if (_ownsLibreHost)
        {
            _libreHost?.Dispose();
        }
    }
}
