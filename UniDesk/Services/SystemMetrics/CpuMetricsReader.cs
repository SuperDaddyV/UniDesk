namespace UniDesk.Services;

public sealed class CpuMetricsReader : ICpuMetricsReader
{
    private readonly Func<double?> _readPerformanceUsage;
    private readonly Func<double?> _readAsusTemperature;
    private readonly Func<CpuMetrics> _readLibreMetrics;
    private readonly IReadOnlyList<IDisposable> _ownedReaders;
    private bool _disposed;

    public CpuMetricsReader()
    {
        var performanceReader = new PerformanceCounterCpuReader();
        var asusReader = new AsusCpuTemperatureReader();
        var libreReader = new LibreHardwareCpuReader();
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
            cpuTemperature ?? fallback.CpuTemperature);
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
