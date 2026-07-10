using UniDesk.Models;

namespace UniDesk.Services;

public sealed class SystemMetricsService : ISystemMetricsService, IDisposable
{
    private readonly ICpuMetricsReader _cpuReader;
    private readonly IGpuMetricsReader _gpuReader;
    private readonly IMemoryMetricsReader _memoryReader;
    private readonly INetworkMetricsReader _networkReader;
    private bool _disposed;

    public SystemMetricsService()
        : this(
            new CpuMetricsReader(),
            new GpuMetricsReader(),
            new WindowsMemoryMetricsReader(),
            new NetworkMetricsReader())
    {
    }

    public SystemMetricsService(
        ICpuMetricsReader cpuReader,
        IGpuMetricsReader gpuReader,
        IMemoryMetricsReader memoryReader,
        INetworkMetricsReader networkReader)
    {
        _cpuReader = cpuReader;
        _gpuReader = gpuReader;
        _memoryReader = memoryReader;
        _networkReader = networkReader;
    }

    public SystemMetricsSnapshot Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cpu = _cpuReader.Read();
        var gpu = _gpuReader.Read();
        var memory = _memoryReader.Read();
        var network = _networkReader.Read();
        return new SystemMetricsSnapshot
        {
            CpuUsage = cpu.CpuUsage,
            CpuTemperature = cpu.CpuTemperature,
            MemoryUsage = memory.UsagePercent,
            GpuUsage = gpu.GpuUsage,
            GpuTemperature = gpu.GpuTemperature,
            NetworkReceivedBytesPerSecond = network.ReceivedBytesPerSecond,
            NetworkSentBytesPerSecond = network.SentBytesPerSecond
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cpuReader.Dispose();
        _gpuReader.Dispose();
        _networkReader.Dispose();
    }
}
