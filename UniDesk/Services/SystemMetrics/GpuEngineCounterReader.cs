using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed record GpuEngineCounterSample(string InstanceName, double Utilization);

public readonly record struct GpuEngineInstanceIdentity(
    string DeviceId,
    int PhysicalAdapter,
    int EngineIndex,
    string EngineType);

public sealed record GpuEngineReaderDiagnosticStatus(
    bool CanAttempt,
    int ConsecutiveFailures,
    DateTimeOffset? NextRetryAtUtc,
    string? LastFailureReason,
    int ActiveCounterCount,
    DateTimeOffset? LastSampleUtc);

public interface IGpuEngineCounter : IDisposable
{
    double NextValue();
}

public interface IGpuEngineCounterSource : IDisposable
{
    IReadOnlyList<string> GetInstanceNames();
    IGpuEngineCounter CreateCounter(string instanceName);
}

public sealed class GpuEngineCounterReader : IDisposable
{
    private const string ProviderName = "Windows GPU Engine";
    private static readonly Regex InstancePattern = new(
        "^pid_[0-9]+_luid_0x(?<high>[0-9a-f]+)_0x(?<low>[0-9a-f]+)_phys_(?<phys>[0-9]+)_eng_(?<engine>[0-9]+)_engtype_(?<type>.+?)(?:#[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly object _sync = new();
    private readonly IGpuEngineCounterSource _source;
    private readonly ReaderFailureBackoff _backoff;
    private readonly Dictionary<string, IGpuEngineCounter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastSampleUtc;
    private bool _disposed;

    public GpuEngineCounterReader()
        : this(new WindowsGpuEngineCounterSource(), TimeSpan.FromMinutes(5))
    {
    }

    public GpuEngineCounterReader(IGpuEngineCounterSource source, TimeSpan retryDelay)
    {
        _source = source;
        _backoff = new ReaderFailureBackoff(retryDelay);
    }

    public GpuEngineReaderDiagnosticStatus DiagnosticStatus
    {
        get
        {
            lock (_sync)
            {
                return new GpuEngineReaderDiagnosticStatus(
                    _backoff.CanAttempt,
                    _backoff.ConsecutiveFailures,
                    _backoff.NextRetryAtUtc,
                    _backoff.LastFailureReason,
                    _counters.Count,
                    _lastSampleUtc);
            }
        }
    }

    public GpuMetrics Read()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_backoff.CanAttempt)
            {
                return Unavailable(HardwareMetricAvailability.Stale, _backoff.LastFailureReason);
            }

            try
            {
                var instanceNames = _source.GetInstanceNames()
                    .Where(name => TryParseInstanceName(name, out _))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (instanceNames.Length == 0)
                {
                    _backoff.RecordFailure("GPU Engine counter instances were not found.");
                    return Unavailable(
                        HardwareMetricAvailability.NoSensor,
                        "GPU Engine counter instances were not found.");
                }

                RemoveMissingCounters(instanceNames);
                var samples = new List<GpuEngineCounterSample>();
                foreach (var instanceName in instanceNames)
                {
                    if (!_counters.TryGetValue(instanceName, out var counter))
                    {
                        try
                        {
                            counter = _source.CreateCounter(instanceName);
                            _counters.Add(instanceName, counter);
                            _ = counter.NextValue();
                        }
                        catch
                        {
                            counter?.Dispose();
                            _counters.Remove(instanceName);
                        }
                        continue;
                    }

                    try
                    {
                        samples.Add(new GpuEngineCounterSample(instanceName, counter.NextValue()));
                    }
                    catch
                    {
                        counter.Dispose();
                        _counters.Remove(instanceName);
                    }
                }

                _backoff.RecordSuccess();
                var adapters = AggregateSamples(samples);
                if (adapters.Count == 0)
                {
                    return Unavailable(
                        HardwareMetricAvailability.NoSensor,
                        "GPU Engine counters are warming up or have no valid samples.");
                }

                _lastSampleUtc = DateTimeOffset.UtcNow;
                return adapters
                    .OrderByDescending(item => item.GpuUsage)
                    .ThenBy(item => item.UsageDeviceId, StringComparer.Ordinal)
                    .First();
            }
            catch (Exception ex)
            {
                var reason = $"{ex.GetType().Name}: {ex.Message}";
                _backoff.RecordFailure(reason);
                return Unavailable(HardwareMetricAvailability.ProviderUnavailable, reason);
            }
        }
    }

    public static bool TryParseInstanceName(
        string instanceName,
        out GpuEngineInstanceIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        var match = InstancePattern.Match(instanceName);
        if (!match.Success ||
            !uint.TryParse(match.Groups["high"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var high) ||
            !uint.TryParse(match.Groups["low"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var low) ||
            !int.TryParse(match.Groups["phys"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var physicalAdapter) ||
            !int.TryParse(match.Groups["engine"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var engineIndex))
        {
            return false;
        }

        identity = new GpuEngineInstanceIdentity(
            $"luid:{high:X8}:{low:X8}",
            physicalAdapter,
            engineIndex,
            match.Groups["type"].Value);
        return true;
    }

    public static IReadOnlyList<GpuMetrics> AggregateSamples(
        IEnumerable<GpuEngineCounterSample> samples)
    {
        var parsed = samples
            .Where(sample => SensorSelection.IsValidPercentage(sample.Utilization))
            .Select(sample =>
            {
                var success = TryParseInstanceName(sample.InstanceName, out var identity);
                return new { Success = success, Identity = identity, sample.Utilization };
            })
            .Where(item => item.Success)
            .ToList();

        var engines = parsed
            .GroupBy(item => new
            {
                item.Identity.DeviceId,
                item.Identity.PhysicalAdapter,
                item.Identity.EngineIndex,
                item.Identity.EngineType
            })
            .Select(group => new
            {
                group.Key.DeviceId,
                group.Key.PhysicalAdapter,
                Utilization = Math.Min(100, group.Sum(item => item.Utilization))
            });

        return engines
            .GroupBy(item => new { item.DeviceId, item.PhysicalAdapter })
            .Select(group => new GpuMetrics(
                group.Max(item => item.Utilization),
                null,
                ProviderName,
                70,
                false,
                usageSource: ProviderName,
                usageDeviceId: group.Key.DeviceId,
                temperatureAvailability: HardwareMetricAvailability.ProviderUnavailable))
            .ToArray();
    }

    private static GpuMetrics Unavailable(
        HardwareMetricAvailability availability,
        string? reason) =>
        new(
            null,
            null,
            ProviderName,
            70,
            false,
            usageAvailability: availability,
            usageReason: reason,
            temperatureAvailability: HardwareMetricAvailability.ProviderUnavailable);

    private void RemoveMissingCounters(IReadOnlyCollection<string> instanceNames)
    {
        var active = new HashSet<string>(instanceNames, StringComparer.OrdinalIgnoreCase);
        foreach (var missing in _counters.Keys.Where(name => !active.Contains(name)).ToArray())
        {
            _counters[missing].Dispose();
            _counters.Remove(missing);
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
            foreach (var counter in _counters.Values)
            {
                counter.Dispose();
            }

            _counters.Clear();
            _source.Dispose();
        }
    }

    private sealed class WindowsGpuEngineCounterSource : IGpuEngineCounterSource
    {
        private readonly PerformanceCounterCategory _category = new("GPU Engine");

        public IReadOnlyList<string> GetInstanceNames() => _category.GetInstanceNames();

        public IGpuEngineCounter CreateCounter(string instanceName) =>
            new WindowsGpuEngineCounter(instanceName);

        public void Dispose()
        {
        }
    }

    private sealed class WindowsGpuEngineCounter : IGpuEngineCounter
    {
        private readonly PerformanceCounter _counter;

        public WindowsGpuEngineCounter(string instanceName)
        {
            _counter = new PerformanceCounter(
                "GPU Engine",
                "Utilization Percentage",
                instanceName,
                readOnly: true);
        }

        public double NextValue() => _counter.NextValue();
        public void Dispose() => _counter.Dispose();
    }
}
