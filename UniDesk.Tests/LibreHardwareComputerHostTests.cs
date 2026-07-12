using UniDesk.Services;

namespace UniDesk.Tests;

public class LibreHardwareComputerHostTests
{
    [Fact]
    public void LibreCpuReader_ShouldSelectMetricsFromDetachedSnapshot()
    {
        using var host = new FakeLibreHardwareHost(
        [
            new("cpu:0", "Intel Core", HardwareSensorDeviceType.Cpu, "CPU Total", "Load", 35),
            new("cpu:0", "Intel Core", HardwareSensorDeviceType.Cpu, "CPU Package", "Temperature", 62)
        ]);
        using var reader = new LibreHardwareCpuReader(host);

        var metrics = reader.Read();

        Assert.Equal(35, metrics.CpuUsage);
        Assert.Equal(62, metrics.CpuTemperature);
        Assert.Equal("LibreHardwareMonitor", metrics.UsageSource);
        Assert.Equal("cpu:0", metrics.UsageDeviceId);
        Assert.Equal("LibreHardwareMonitor CPU", metrics.TemperatureSource);
        Assert.Equal("cpu:0", metrics.TemperatureDeviceId);
    }

    [Fact]
    public void LibreGpuReader_ShouldKeepValuesScopedToOneDetachedDevice()
    {
        using var host = new FakeLibreHardwareHost(
        [
            new("gpu:0", "NVIDIA GPU", HardwareSensorDeviceType.GpuNvidia, "GPU 3D", "Load", 41),
            new("gpu:0", "NVIDIA GPU", HardwareSensorDeviceType.GpuNvidia, "GPU Core", "Temperature", 65)
        ]);
        using var reader = new LibreHardwareGpuReader(host);

        var metrics = reader.Read();

        Assert.Equal(41, metrics.GpuUsage);
        Assert.Equal(65, metrics.GpuTemperature);
        Assert.Equal("gpu:0", metrics.UsageDeviceId);
        Assert.Equal("gpu:0", metrics.TemperatureDeviceId);
    }

    [Fact]
    public void SystemMetricsService_ShouldRefreshSharedHostOncePerSampleAndDisposeItOnce()
    {
        var host = new FakeLibreHardwareHost([]);
        var cpu = new StubCpuReader();
        var gpu = new StubGpuReader();
        var memory = new StubMemoryReader();
        var network = new StubNetworkReader();
        var service = new SystemMetricsService(cpu, gpu, memory, network, host, ownsLibreHost: true);

        _ = service.Read();
        service.Dispose();
        service.Dispose();

        Assert.Equal(1, host.RefreshCount);
        Assert.Equal(1, host.DisposeCount);
    }

    [Fact]
    public void SystemMetricsService_DiagnosticsShouldKeepOnlyLastThreeSnapshots()
    {
        using var host = new FakeLibreHardwareHost([]);
        using var service = new SystemMetricsService(
            new StubCpuReader(),
            new StubGpuReader(),
            new StubMemoryReader(),
            new StubNetworkReader(),
            host);

        _ = service.Read();
        _ = service.Read();
        _ = service.Read();
        _ = service.Read();

        Assert.Equal(3, service.CaptureDiagnostics().RecentSnapshots.Count);
    }

    private sealed class FakeLibreHardwareHost : ILibreHardwareComputerHost
    {
        public FakeLibreHardwareHost(IReadOnlyList<HardwareSensorSnapshot> sensors)
        {
            CurrentSensors = sensors;
        }

        public int RefreshCount { get; private set; }
        public int DisposeCount { get; private set; }
        public IReadOnlyList<HardwareSensorSnapshot> CurrentSensors { get; }
        public LibreHardwareHostDiagnosticStatus DiagnosticStatus { get; } =
            new(true, false, null, null, []);

        public void Refresh() => RefreshCount++;
        public void Dispose() => DisposeCount++;
    }

    private sealed class StubCpuReader : ICpuMetricsReader
    {
        public CpuMetrics Read() => CpuMetrics.Empty;
        public void Dispose() { }
    }

    private sealed class StubGpuReader : IGpuMetricsReader
    {
        public GpuMetrics Read() => GpuMetrics.Empty;
        public void Dispose() { }
    }

    private sealed class StubMemoryReader : IMemoryMetricsReader
    {
        public MemoryMetrics Read() => new(0, 0, 0, 0);
    }

    private sealed class StubNetworkReader : INetworkMetricsReader
    {
        public NetworkMetrics Read() => new(0, 0);
        public void Dispose() { }
    }
}
