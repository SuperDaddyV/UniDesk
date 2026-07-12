namespace UniDesk.Services;

public sealed class GpuMetricsReader : IGpuMetricsReader
{
    private readonly Func<GpuMetrics> _readNvidia;
    private readonly Func<GpuMetrics> _readAmd;
    private readonly Func<GpuMetrics> _readGpuEngine;
    private readonly Func<GpuMetrics> _readLibre;
    private readonly GpuEngineCounterReader? _gpuEngineReader;
    private readonly IReadOnlyList<IDisposable> _ownedReaders;
    private bool _disposed;

    public GpuMetricsReader()
        : this(new LibreHardwareGpuReader())
    {
    }

    public GpuMetricsReader(ILibreHardwareComputerHost libreHost)
        : this(new LibreHardwareGpuReader(libreHost))
    {
    }

    private GpuMetricsReader(LibreHardwareGpuReader libreReader)
    {
        var nvidiaReader = new NvidiaNvmlGpuReader();
        var amdReader = new AmdAdlGpuReader();
        var gpuEngineReader = new GpuEngineCounterReader();
        _gpuEngineReader = gpuEngineReader;
        _readNvidia = nvidiaReader.Read;
        _readAmd = amdReader.Read;
        _readGpuEngine = gpuEngineReader.Read;
        _readLibre = libreReader.Read;
        _ownedReaders = [nvidiaReader, gpuEngineReader, libreReader];
    }

    public GpuMetricsReader(
        Func<GpuMetrics> readNvidia,
        Func<GpuMetrics> readAmd,
        Func<GpuMetrics> readLibre)
    {
        _readNvidia = readNvidia;
        _readAmd = readAmd;
        _readGpuEngine = () => GpuMetrics.Empty;
        _readLibre = readLibre;
        _ownedReaders = [];
    }

    public GpuMetricsReader(
        Func<GpuMetrics> readNvidia,
        Func<GpuMetrics> readAmd,
        Func<GpuMetrics> readGpuEngine,
        Func<GpuMetrics> readLibre)
    {
        _readNvidia = readNvidia;
        _readAmd = readAmd;
        _readGpuEngine = readGpuEngine;
        _readLibre = readLibre;
        _ownedReaders = [];
    }

    public GpuEngineReaderDiagnosticStatus? GpuEngineDiagnosticStatus =>
        _gpuEngineReader?.DiagnosticStatus;

    public GpuMetrics Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var candidates = new List<GpuMetrics>
        {
            _readNvidia(),
            _readAmd()
        };

        if (!candidates.Any(candidate => candidate.HasAllValues))
        {
            candidates.Add(_readGpuEngine());
        }

#if DEBUG
        candidates.Add(_readLibre());
#else
        if (!candidates.Any(candidate => candidate.HasAllValues))
        {
            candidates.Add(_readLibre());
        }
#endif

        return SelectMetrics(candidates);
    }

    internal static GpuMetrics SelectMetrics(IEnumerable<GpuMetrics> candidates)
    {
        var validCandidates = candidates
            .Where(candidate => candidate.HasAnyValue)
            .ToList();
        if (validCandidates.Count == 0)
        {
            return GpuMetrics.Empty;
        }

        var selected = validCandidates
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .First();
        if (selected.HasAllValues)
        {
            return selected;
        }

        var anchorDeviceId = selected.GpuUsage.HasValue
            ? selected.UsageDeviceId
            : selected.TemperatureDeviceId;
        var usageCandidate = selected.GpuUsage.HasValue
            ? selected
            : SelectBestUsageCandidate(validCandidates, anchorDeviceId);
        var temperatureCandidate = selected.GpuTemperature.HasValue
            ? selected
            : SelectBestTemperatureCandidate(validCandidates, anchorDeviceId);

        return new GpuMetrics(
            usageCandidate?.GpuUsage,
            temperatureCandidate?.GpuTemperature,
            selected.SourceName,
            selected.SourcePriority,
            selected.IsDiscrete,
            usageSource: usageCandidate?.UsageSource,
            usageDeviceId: usageCandidate?.UsageDeviceId,
            temperatureSource: temperatureCandidate?.TemperatureSource,
            temperatureDeviceId: temperatureCandidate?.TemperatureDeviceId,
            usageAvailability: usageCandidate?.UsageAvailability,
            temperatureAvailability: temperatureCandidate?.TemperatureAvailability,
            usageReason: usageCandidate?.UsageReason,
            temperatureReason: temperatureCandidate?.TemperatureReason);
    }

    private static GpuMetrics? SelectBestUsageCandidate(
        IEnumerable<GpuMetrics> candidates,
        string? requiredDeviceId) =>
        candidates
            .Where(candidate =>
                candidate.GpuUsage.HasValue &&
                IsSameKnownDevice(requiredDeviceId, candidate.UsageDeviceId))
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Cast<GpuMetrics?>()
            .FirstOrDefault();

    private static GpuMetrics? SelectBestTemperatureCandidate(
        IEnumerable<GpuMetrics> candidates,
        string? requiredDeviceId) =>
        candidates
            .Where(candidate =>
                candidate.GpuTemperature.HasValue &&
                IsSameKnownDevice(requiredDeviceId, candidate.TemperatureDeviceId))
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Cast<GpuMetrics?>()
            .FirstOrDefault();

    private static bool IsSameKnownDevice(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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
