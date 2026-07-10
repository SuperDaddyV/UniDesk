namespace UniDesk.Services;

public sealed class GpuMetricsReader : IGpuMetricsReader
{
    private readonly Func<GpuMetrics> _readNvidia;
    private readonly Func<GpuMetrics> _readAmd;
    private readonly Func<GpuMetrics> _readLibre;
    private readonly IReadOnlyList<IDisposable> _ownedReaders;
    private bool _disposed;

    public GpuMetricsReader()
    {
        var nvidiaReader = new NvidiaNvmlGpuReader();
        var amdReader = new AmdAdlGpuReader();
        var libreReader = new LibreHardwareGpuReader();
        _readNvidia = nvidiaReader.Read;
        _readAmd = amdReader.Read;
        _readLibre = libreReader.Read;
        _ownedReaders = [nvidiaReader, libreReader];
    }

    public GpuMetricsReader(
        Func<GpuMetrics> readNvidia,
        Func<GpuMetrics> readAmd,
        Func<GpuMetrics> readLibre)
    {
        _readNvidia = readNvidia;
        _readAmd = readAmd;
        _readLibre = readLibre;
        _ownedReaders = [];
    }

    public GpuMetrics Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var candidates = new List<GpuMetrics>
        {
            _readNvidia(),
            _readAmd()
        };

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

        return new GpuMetrics(
            selected.GpuUsage ?? SelectBestUsage(validCandidates),
            selected.GpuTemperature ?? SelectBestTemperature(validCandidates),
            selected.SourceName,
            selected.SourcePriority,
            selected.IsDiscrete);
    }

    private static double? SelectBestUsage(IEnumerable<GpuMetrics> candidates) =>
        candidates
            .Where(candidate => candidate.GpuUsage.HasValue)
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Select(candidate => candidate.GpuUsage)
            .FirstOrDefault();

    private static double? SelectBestTemperature(IEnumerable<GpuMetrics> candidates) =>
        candidates
            .Where(candidate => candidate.GpuTemperature.HasValue)
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Select(candidate => candidate.GpuTemperature)
            .FirstOrDefault();

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
