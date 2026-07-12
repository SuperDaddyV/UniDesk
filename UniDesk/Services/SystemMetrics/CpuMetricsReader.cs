namespace UniDesk.Services;

public sealed class CpuMetricsReader : ICpuMetricsReader
{
    private readonly Func<double?> _readPerformanceUsage;
    private readonly Func<double?> _readAsusTemperature;
    private readonly Func<CpuMetrics> _readLibreMetrics;
    private readonly IReadOnlyList<IDisposable> _ownedReaders;
    private bool _disposed;

    public CpuMetricsReader()
        : this(new LibreHardwareCpuReader())
    {
    }

    public CpuMetricsReader(ILibreHardwareComputerHost libreHost)
        : this(new LibreHardwareCpuReader(libreHost))
    {
    }

    private CpuMetricsReader(LibreHardwareCpuReader libreReader)
    {
        var performanceReader = new PerformanceCounterCpuReader();
        var asusReader = new AsusCpuTemperatureReader();
        _readPerformanceUsage = performanceReader.ReadUsage;
        _readAsusTemperature = asusReader.ReadCpuPackageTemperature;
        _readLibreMetrics = libreReader.Read;
        _ownedReaders = [performanceReader, libreReader];
    }

    public CpuMetricsReader(
        Func<double?> readPerformanceUsage,
        Func<double?> readAsusTemperature,
        Func<CpuMetrics> readLibreMetrics)
    {
        _readPerformanceUsage = readPerformanceUsage;
        _readAsusTemperature = readAsusTemperature;
        _readLibreMetrics = readLibreMetrics;
        _ownedReaders = [];
    }

    public CpuMetrics Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cpuUsage = SensorSelection.NormalizePercentage(_readPerformanceUsage());
        var cpuTemperature = SensorSelection.NormalizeTemperature(_readAsusTemperature());
        var fallback = CpuMetrics.Empty;
#if DEBUG
        fallback = _readLibreMetrics();
#else
        if (!cpuUsage.HasValue || !cpuTemperature.HasValue)
        {
            fallback = _readLibreMetrics();
        }
#endif
        return new CpuMetrics(
            cpuUsage ?? fallback.CpuUsage,
            cpuTemperature ?? fallback.CpuTemperature,
            usageSource: cpuUsage.HasValue ? "Windows Performance Counter" : fallback.UsageSource,
            usageDeviceId: cpuUsage.HasValue ? null : fallback.UsageDeviceId,
            temperatureSource: cpuTemperature.HasValue ? "ASUS Armoury Crate" : fallback.TemperatureSource,
            temperatureDeviceId: cpuTemperature.HasValue ? null : fallback.TemperatureDeviceId,
            usageAvailability: cpuUsage.HasValue ? null : fallback.UsageAvailability,
            temperatureAvailability: cpuTemperature.HasValue ? null : fallback.TemperatureAvailability,
            usageReason: cpuUsage.HasValue ? null : fallback.UsageReason,
            temperatureReason: cpuTemperature.HasValue ? null : fallback.TemperatureReason);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var reader in _ownedReaders)
        {
            reader.Dispose();
        }
    }
}
